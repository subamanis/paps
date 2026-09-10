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
            List<PredictiveSuggestion> hereOpening = new List<PredictiveSuggestion>();
            List<PredictiveSuggestion> hereInside = new List<PredictiveSuggestion>();
            List<PredictiveSuggestion> anywhereOpening = new List<PredictiveSuggestion>();
            List<PredictiveSuggestion> anywhereInside = new List<PredictiveSuggestion>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> probed = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (cancellationToken.IsCancellationRequested || hereOpening.Count + hereInside.Count >= Limit)
                {
                    break;
                }

                string line = lines[i];

                if (line.Length <= input.Length)
                {
                    continue;
                }

                bool opening = line.StartsWith(input, StringComparison.OrdinalIgnoreCase);

                if (!opening && line.IndexOf(input, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (!seen.Add(line))
                {
                    continue;
                }

                Reach reach = MeasureReach(line, location, probed);

                if (reach == Reach.Elsewhere)
                {
                    continue;
                }

                List<PredictiveSuggestion> bucket = reach == Reach.Here
                    ? (opening ? hereOpening : hereInside)
                    : (opening ? anywhereOpening : anywhereInside);

                if (bucket.Count < Limit)
                {
                    bucket.Add(new PredictiveSuggestion(line));
                }
            }

            List<PredictiveSuggestion> ranked = new List<PredictiveSuggestion>(Limit);

            foreach (List<PredictiveSuggestion> bucket in new[] { hereOpening, hereInside, anywhereOpening, anywhereInside })
            {
                foreach (PredictiveSuggestion suggestion in bucket)
                {
                    if (ranked.Count == Limit)
                    {
                        break;
                    }

                    ranked.Add(suggestion);
                }
            }

            if (ranked.Count == 0)
            {
                return default;
            }

            return new SuggestionPackage(ranked);
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

        private const int MaximumExtension = 5;

        private const string AncestorMark = "|";

        private static readonly Guid _id = new Guid(Identifier);

        private static readonly object _gate = new object();

        private static readonly char[] _trimmed = { '"', '\'', ',', ';', '(', ')', '`' };

        private static readonly string[] _executable = { ".exe", ".cmd", ".bat", ".ps1", ".com" };

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
                string? pending = null;
                string? line;

                while ((line = reader.ReadLine()) != null)
                {
                    bool continued = line.EndsWith("`", StringComparison.Ordinal);
                    string body = continued ? line.Substring(0, line.Length - 1) : line;

                    if (pending != null)
                    {
                        body = pending.TrimEnd() + " " + body.TrimStart();
                    }

                    if (continued)
                    {
                        pending = body;
                        continue;
                    }

                    pending = null;

                    if (body.Length != 0)
                    {
                        lines.Add(body);
                    }
                }

                if (pending != null && pending.Length != 0)
                {
                    lines.Add(pending);
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

                if (index < line.Length && (line[index] == '"' || line[index] == '\''))
                {
                    char quote = line[index];
                    index++;

                    while (index < line.Length && line[index] != quote)
                    {
                        index++;
                    }

                    if (index < line.Length)
                    {
                        index++;
                    }
                }

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
                    if (!Path.IsPathRooted(token))
                    {
                        reach = Reach.Here;
                    }
                }
                else if (MustBeAPath(token, location, probed))
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

            if (token.IndexOf('@') >= 0)
            {
                return false;
            }

            return token.IndexOf('/') >= 0 || token.IndexOf('\\') >= 0;
        }

        private static bool MustBeAPath(string token, string location, Dictionary<string, bool> probed)
        {
            if (token.IndexOf('\\') >= 0 || Path.IsPathRooted(token))
            {
                return true;
            }

            if (token.StartsWith("./", StringComparison.Ordinal) || token.StartsWith("../", StringComparison.Ordinal))
            {
                return true;
            }

            if (HasFileExtension(token))
            {
                return true;
            }

            return HasResolvingAncestor(token, location, probed);
        }

        private static bool HasFileExtension(string token)
        {
            int slash = token.LastIndexOf('/');
            string last = slash >= 0 ? token.Substring(slash + 1) : token;
            int dot = last.LastIndexOf('.');

            if (dot <= 0 || dot == last.Length - 1)
            {
                return false;
            }

            string extension = last.Substring(dot + 1);

            if (extension.Length > MaximumExtension)
            {
                return false;
            }

            foreach (char letter in extension)
            {
                if (!char.IsLetter(letter))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasResolvingAncestor(string token, string location, Dictionary<string, bool> probed)
        {
            int slash = token.IndexOf('/');

            while (slash > 0)
            {
                if (Exists(token.Substring(0, slash), location, probed, false))
                {
                    return true;
                }

                slash = token.IndexOf('/', slash + 1);
            }

            return false;
        }

        private static bool Exists(string token, string location, Dictionary<string, bool> probed, bool executable = true)
        {
            string key = executable ? token : AncestorMark + token;

            if (probed.TryGetValue(key, out bool known))
            {
                return known;
            }

            bool found = false;

            try
            {
                string full = Path.IsPathRooted(token) ? token : Path.Combine(location, token);
                found = File.Exists(full) || Directory.Exists(full);

                if (!found && executable && !HasFileExtension(token))
                {
                    foreach (string extension in _executable)
                    {
                        if (File.Exists(full + extension))
                        {
                            found = true;
                            break;
                        }
                    }
                }
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }

            probed[key] = found;

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
