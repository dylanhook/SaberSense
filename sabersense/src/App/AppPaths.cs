// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using IPA.Utilities;
using System.IO;

namespace SaberSense.App;

public class AppPaths
{
    private const string SaberFolderName = "CustomSabers";

    public DirectoryInfo SaberRoot { get; }

    public DirectoryInfo DataRoot { get; }

    public DirectoryInfo ConfigsRoot { get; }

    public AppPaths()
    {
        SaberRoot = Directory.CreateDirectory(
        Path.Combine(UnityGame.InstallPath, SaberFolderName));

        DataRoot = Directory.CreateDirectory(
        Path.Combine(UnityGame.UserDataPath, "SaberSense"));

        ConfigsRoot = Directory.CreateDirectory(
        Path.Combine(DataRoot.FullName, "Configs"));
    }
}