﻿// ------------------------------------------------------------------------
// Info.cs written by Code A Software (http://www.code-a-software.net)
// All rights reserved
// Created on:      11.04.2016
// Last update on:  02.08.2016
// ------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VFS.Application.GUI.Tab
{
    public struct Info
    {
        public int CurrentSite;
        public int MaxSite;

        public Info(int CurrentSite, int MaxSite)
        {
            this.CurrentSite = CurrentSite;
            this.MaxSite = MaxSite;
        }
    }
}
