using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

public class LogTab : Tab
{
    private FileSystemWatcher? _watcher;

    private List<LogEntry>? _log;

    private string LogFileFullPath => Path.GetFullPath(Everest.PathLog ?? "");
    
    public override string Name => "Log";
    
    public override void Render(Level level)
    {
        if (Everest.PathLog is not { })
        {
            ImGui.Text("The Everest log is disabled.");
            return;
        }
        
        if (_watcher is null && Everest.PathLog is { } logFile)
        {
            var logFileFull = Path.GetFullPath(logFile);
            _watcher = new(Path.GetDirectoryName(logFileFull)!.Replace('/', Path.DirectorySeparatorChar));
            //_watcher.Filter = logFileFull;
            _watcher.NotifyFilter = NotifyFilters.Attributes
                                    | NotifyFilters.CreationTime
                                    | NotifyFilters.DirectoryName
                                    | NotifyFilters.FileName
                                    | NotifyFilters.LastAccess
                                    | NotifyFilters.LastWrite
                                    | NotifyFilters.Security
                                    | NotifyFilters.Size;
            
            
            _watcher.Changed += WatcherOnChanged;
            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;
        }

        var logExisted = _log is { };
        
        _log ??= ReadLog();

        
        
        if (_log is { } log)
        {
            ImGui.BeginChild("##log");
            
            foreach (var entry in log)
            {
                var col = entry.LogLevel switch
                {
                    LogLevel.Verbose => Color.Gray,
                    LogLevel.Debug => Color.LightGray,
                    LogLevel.Info => Color.White,
                    LogLevel.Warn => Color.Yellow,
                    LogLevel.Error => Color.Red,
                    _ => Color.White,
                };
                ImGui.PushStyleColor(ImGuiCol.Text, col.ToNumVec4());
                ImGui.TextWrapped(entry.Line);
                ImGui.PopStyleColor(1);
            }
            
            if (ImGui.Button("Reload"))
                _log = null;
            
            if (!logExisted)
                ImGui.SetScrollHereY();
            
            ImGui.EndChild();
        }
    }

    private void WatcherOnChanged(object sender, FileSystemEventArgs e)
    {
        _log = null;
    }

    private List<LogEntry> ReadLog()
    {
        if (Everest.PathLog is not { })
        {
            return [ new("NO LOG") ];
        }

        try
        {
            //var file = File.ReadAllLines(LogFileFullPath);
            using var fileStream = File.Open(LogFileFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream);
            var fileFull = reader.ReadToEnd();
            
            return fileFull.Split(Environment.NewLine).Select(f => new LogEntry(f)).ToList();
        }
        catch (Exception ex)
        {
            return [ new(ex.ToString()) ];
        }
    }

    private record LogEntry
    {
        private static readonly Regex LogLineRegex = new(@"^\(.+?\) \[(.+?)\] \[(.+?)\] \[(.+?)\]", RegexOptions.Compiled);
        
        public LogLevel LogLevel { get; }
        
        public string Line { get; }

        public LogEntry(string line)
        {
            Line = line;

            if (LogLineRegex.Match(line) is { Success: true } match)
            {
                LogLevel = match.Groups[2].ValueSpan switch
                {
                    "Warn" => LogLevel.Warn,
                    "Info" => LogLevel.Info,
                    "Debug" => LogLevel.Debug,
                    "Error" => LogLevel.Error,
                    "Verbose" => LogLevel.Verbose,
                    _ => LogLevel.Error,
                };
            }
            else
            {
                LogLevel = LogLevel.Verbose;
            }
        }
    }
}