using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Prediction;
using System.Threading;

namespace ContextHistoryPredictor
{
    public sealed class ContextHistory : ICommandPredictor
    {
        public const string Identifier = "3f7c9a12-6d84-4b0e-9c31-5ae8d2f60417";

        public static volatile string? Location;

        public static volatile string? HistoryPath;

        public Guid Id => _id;

        public string Name => "ContextHistory";

        public string Description => "Command history filtered to the directory you are standing in.";

        public SuggestionPackage GetSuggestion(PredictionClient client, PredictionContext context, CancellationToken cancellationToken)
        {
            string input = context.InputAst.Extent.Text;
            string? location = Location;

            if (input.Length < MinimumInput || string.IsNullOrWhiteSpace(input) || string.IsNullOrEmpty(location))
            {
                return default;
            }

            string[] lines = ReadHistory();
            List<PredictiveSuggestion> here = new List<PredictiveSuggestion>();
            List<PredictiveSuggestion> anywhere = new List<PredictiveSuggestion>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> probed = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (cancellationToken.IsCancellationRequested || here.Count + anywhere.Count >= Limit)
                {
                    break;
                }

                string line = lines[i];

                if (line.Length <= input.Length || !line.StartsWith(input, StringComparison.OrdinalIgnoreCase) || !seen.Add(line))
                {
                    continue;
                }

                Reach reach = MeasureReach(line, location, probed);

                if (reach == Reach.Elsewhere)
                {
                    continue;
                }

                if (reach == Reach.Here)
                {
                    here.Add(new PredictiveSuggestion(line));
                }
                else
                {
                    anywhere.Add(new PredictiveSuggestion(line));
                }
            }

            if (here.Count == 0 && anywhere.Count == 0)
            {
                return default;
            }

            here.AddRange(anywhere);

            return new SuggestionPackage(here);
        }

        public bool CanAcceptFeedback(PredictionClient client, PredictorFeedbackKind feedback) => false;

        public void OnCommandLineAccepted(PredictionClient client, IReadOnlyList<string> history)
        {
        }

        public void OnCommandLineExecuted(PredictionClient client, string commandLine, bool success)
        {
        }

        public void OnSuggestionAccepted(PredictionClient client, uint session, string acceptedSuggestion)
        {
        }

        public void OnSuggestionDisplayed(PredictionClient client, uint session, int countOrIndex)
        {
        }

        private const int Limit = 10;

        private const int MinimumInput = 2;

        private static readonly Guid _id = new Guid(Identifier);

        private static readonly object _gate = new object();

        private static readonly char[] _trimmed = { '"', '\'', ',', ';', '(', ')', '`' };

        private static string[] _lines = Array.Empty<string>();

        private static long _stamp = -1;

        private enum Reach
        {
            Anywhere,
            Here,
            Elsewhere,
        }

        private static string[] ReadHistory()
        {
            string? path = HistoryPath;

            if (string.IsNullOrEmpty(path))
            {
                return Array.Empty<string>();
            }

            FileInfo info = new FileInfo(path);

            if (!info.Exists)
            {
                return Array.Empty<string>();
            }

            long stamp = info.Length ^ info.LastWriteTimeUtc.Ticks;

            lock (_gate)
            {
                if (stamp == _stamp)
                {
                    return _lines;
                }

                try
                {
                    _lines = ReadLines(path);
                    _stamp = stamp;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                return _lines;
            }
        }

        private static string[] ReadLines(string path)
        {
            List<string> lines = new List<string>();

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader reader = new StreamReader(stream))
            {
                bool skipping = false;
                string? line;

                while ((line = reader.ReadLine()) != null)
                {
                    bool continued = line.EndsWith("`", StringComparison.Ordinal);

                    if (skipping)
                    {
                        skipping = continued;
                        continue;
                    }

                    if (continued)
                    {
                        skipping = true;
                        continue;
                    }

                    if (line.Length != 0)
                    {
                        lines.Add(line);
                    }
                }
            }

            return lines.ToArray();
        }

        private static Reach MeasureReach(string line, string location, Dictionary<string, bool> probed)
        {
            Reach reach = Reach.Anywhere;
            int index = 0;

            while (index < line.Length)
            {
                while (index < line.Length && char.IsWhiteSpace(line[index]))
                {
                    index++;
                }

                int start = index;

                while (index < line.Length && !char.IsWhiteSpace(line[index]))
                {
                    index++;
                }

                if (index == start)
                {
                    break;
                }

                string token = line.Substring(start, index - start).Trim(_trimmed);

                if (!MayBeAPath(token))
                {
                    continue;
                }

                if (Exists(token, location, probed))
                {
                    reach = Reach.Here;
                }
                else if (MustBeAPath(token))
                {
                    return Reach.Elsewhere;
                }
            }

            return reach;
        }

        private static bool MayBeAPath(string token)
        {
            if (token.Length < 2 || token[0] == '-')
            {
                return false;
            }

            if (token.StartsWith("\\\\", StringComparison.Ordinal) || token.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            return token.IndexOf('/') >= 0 || token.IndexOf('\\') >= 0;
        }

        private static bool MustBeAPath(string token)
        {
            if (token.IndexOf('\\') >= 0 || Path.IsPathRooted(token))
            {
                return true;
            }

            return token.StartsWith("./", StringComparison.Ordinal) || token.StartsWith("../", StringComparison.Ordinal);
        }

        private static bool Exists(string token, string location, Dictionary<string, bool> probed)
        {
            if (probed.TryGetValue(token, out bool known))
            {
                return known;
            }

            bool found = false;

            try
            {
                string full = Path.IsPathRooted(token) ? token : Path.Combine(location, token);
                found = File.Exists(full) || Directory.Exists(full);
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }

            probed[token] = found;

            return found;
        }
    }

    public sealed class Startup : IModuleAssemblyInitializer, IModuleAssemblyCleanup
    {
        public void OnImport()
        {
            SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, new ContextHistory());
        }

        public void OnRemove(PSModuleInfo module)
        {
            SubsystemManager.UnregisterSubsystem(SubsystemKind.CommandPredictor, new Guid(ContextHistory.Identifier));
        }
    }
}
