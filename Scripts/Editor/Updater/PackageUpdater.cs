// Existing content of PackageUpdater.cs

// Modified method: BuildPlanRoutine
private void BuildPlanRoutine()
{
    // Always compute filesToDelete
    filesToDelete = FindMissingLocalFiles(remoteFiles);
    
    // Rest of your logic...
}

// Modified method: ApplyPendingState
private void ApplyPendingState()
{
    foreach (var file in pending.filesToDelete)
    {
        // Delete the file
        DeleteFile(file);
        
        // Also delete the .meta file if it exists
        var metaFile = file + ".meta";
        if (File.Exists(metaFile))
        {
            DeleteFile(metaFile);
        }
    }
    
    // Rest of your logic...
}