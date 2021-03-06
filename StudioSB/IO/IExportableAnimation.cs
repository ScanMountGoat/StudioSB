﻿using System;
using StudioSB.Scenes;
using StudioSB.Scenes.Animation;

namespace StudioSB.IO
{
    interface IExportableAnimation
    {
        string Name { get; }
        string Extension { get; }

        void ExportSBAnimation(string FileName, SBAnimation animation, SBSkeleton skeleton);
    }
}
