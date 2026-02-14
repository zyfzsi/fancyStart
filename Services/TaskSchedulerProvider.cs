#nullable enable

using System.IO;
using FancyStart.Models;

namespace FancyStart.Services;

public class TaskSchedulerProvider : IStartupProvider
{
    private const int TASK_TRIGGER_LOGON = 9;
    private const int TASK_ACTION_EXEC = 0;
    private const int TASK_CREATE_OR_UPDATE = 6;
    private const int TASK_LOGON_INTERACTIVE_TOKEN = 3;
    private const int TASK_RUNLEVEL_HIGHEST = 1;

    public List<StartupItem> GetStartupItems()
    {
        var items = new List<StartupItem>();

        try
        {
            dynamic scheduler = CreateSchedulerService();
            dynamic rootFolder = scheduler.GetFolder("\\");
            CollectTasksRecursive(rootFolder, items);
        }
        catch
        {
            // Task Scheduler service may not be available
        }

        return items;
    }

    public void Enable(StartupItem item)
    {
        try
        {
            dynamic scheduler = CreateSchedulerService();
            dynamic task = scheduler.GetFolder("\\").GetTask(item.TaskPath);
            dynamic definition = task.Definition;
            definition.Settings.Enabled = true;
            var folderPath = GetFolderPath(item.TaskPath);
            dynamic folder = scheduler.GetFolder(folderPath);
            folder.RegisterTaskDefinition(
                task.Name,
                definition,
                TASK_CREATE_OR_UPDATE,
                null,
                null,
                TASK_LOGON_INTERACTIVE_TOKEN);

            item.IsEnabled = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to enable scheduled task '{item.Name}'.", ex);
        }
    }

    public void Disable(StartupItem item)
    {
        try
        {
            dynamic scheduler = CreateSchedulerService();
            dynamic task = scheduler.GetFolder("\\").GetTask(item.TaskPath);
            dynamic definition = task.Definition;
            definition.Settings.Enabled = false;
            var folderPath = GetFolderPath(item.TaskPath);
            dynamic folder = scheduler.GetFolder(folderPath);
            folder.RegisterTaskDefinition(
                task.Name,
                definition,
                TASK_CREATE_OR_UPDATE,
                null,
                null,
                TASK_LOGON_INTERACTIVE_TOKEN);

            item.IsEnabled = false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to disable scheduled task '{item.Name}'.", ex);
        }
    }

    public void Delete(StartupItem item)
    {
        try
        {
            dynamic scheduler = CreateSchedulerService();
            var folderPath = GetFolderPath(item.TaskPath);
            var taskName = GetTaskName(item.TaskPath);
            dynamic folder = scheduler.GetFolder(folderPath);
            folder.DeleteTask(taskName, 0);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete scheduled task '{item.Name}'.", ex);
        }
    }

    public void Add(string filePath)
    {
        try
        {
            dynamic scheduler = CreateSchedulerService();
            dynamic definition = scheduler.NewTask(0);

            definition.RegistrationInfo.Description =
                $"Startup task for {Path.GetFileNameWithoutExtension(filePath)}";

            // Add a logon trigger
            dynamic trigger = definition.Triggers.Create(TASK_TRIGGER_LOGON);
            trigger.Enabled = true;

            // Add an exec action
            dynamic action = definition.Actions.Create(TASK_ACTION_EXEC);
            action.Path = filePath;
            action.WorkingDirectory = Path.GetDirectoryName(filePath) ?? string.Empty;

            definition.Settings.Enabled = true;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.DisallowStartIfOnBatteries = false;

            // Run with highest privileges so admin-required apps can start
            definition.Principal.RunLevel = TASK_RUNLEVEL_HIGHEST;

            var taskName = Path.GetFileNameWithoutExtension(filePath);
            dynamic rootFolder = scheduler.GetFolder("\\");
            rootFolder.RegisterTaskDefinition(
                taskName,
                definition,
                TASK_CREATE_OR_UPDATE,
                null,
                null,
                TASK_LOGON_INTERACTIVE_TOKEN);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to add scheduled task for '{filePath}'.", ex);
        }
    }

    private static dynamic CreateSchedulerService()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service")
            ?? throw new InvalidOperationException("Task Scheduler COM class not found.");

        dynamic scheduler = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Failed to create Task Scheduler instance.");

        scheduler.Connect();
        return scheduler;
    }

    private static void CollectTasksRecursive(dynamic folder, List<StartupItem> items)
    {
        try
        {
            foreach (dynamic task in folder.GetTasks(0))
            {
                try
                {
                    if (!HasLogonTrigger(task))
                        continue;

                    var command = string.Empty;
                    var arguments = string.Empty;

                    // Get the first exec action
                    dynamic actions = task.Definition.Actions;
                    if (actions.Count > 0)
                    {
                        dynamic firstAction = actions.Item[1]; // 1-based index
                        if (firstAction.Type == TASK_ACTION_EXEC)
                        {
                            command = (string)(firstAction.Path ?? string.Empty);
                            arguments = (string)(firstAction.Arguments ?? string.Empty);
                        }
                    }

                    items.Add(new StartupItem
                    {
                        Name = (string)task.Name,
                        Command = command,
                        Arguments = arguments,
                        IsEnabled = (bool)task.Enabled,
                        Source = StartupSourceType.TaskScheduler,
                        SourceDetail = (string)task.Path,
                        TaskPath = (string)task.Path,
                        IconPath = command,
                    });
                }
                catch
                {
                    // Skip individual tasks that fail
                }
            }
        }
        catch
        {
            // Skip tasks enumeration if it fails
        }

        // Recurse into subfolders
        try
        {
            foreach (dynamic subFolder in folder.GetFolders(0))
            {
                try
                {
                    CollectTasksRecursive(subFolder, items);
                }
                catch
                {
                    // Skip subfolders that fail
                }
            }
        }
        catch
        {
            // Skip folder enumeration if it fails
        }
    }

    private static bool HasLogonTrigger(dynamic task)
    {
        try
        {
            foreach (dynamic trigger in task.Definition.Triggers)
            {
                if (trigger.Type == TASK_TRIGGER_LOGON)
                    return true;
            }
        }
        catch
        {
            // If we can't read triggers, skip this task
        }

        return false;
    }

    private static string GetFolderPath(string taskPath)
    {
        var lastSep = taskPath.LastIndexOf('\\');
        return lastSep > 0 ? taskPath[..lastSep] : "\\";
    }

    private static string GetTaskName(string taskPath)
    {
        var lastSep = taskPath.LastIndexOf('\\');
        return lastSep >= 0 ? taskPath[(lastSep + 1)..] : taskPath;
    }
}
