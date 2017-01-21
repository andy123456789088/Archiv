﻿// ------------------------------------------------------------------------
// ExtendedVFST.cs written by Code A Software (http://www.code-a-software.net)
// SP: VHP-0001 (OpenSource-Software)
// Created on:      27.12.2016
// Last update on:  21.01.2017
// ------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using VFS.Interfaces;
using System.IO;
using System.ComponentModel;

namespace VFS.ExtendedVFS.Wrapper
{
    /// <summary>
    /// Thread-based and exception handling ModifiedVFS
    /// </summary>
    public class ExtendedVFST : VFS
    {
        private BackgroundWorker currentWorker = null;
        private bool isBusy = false;
        private Result result = null;
        private Stopwatch stw = null;
        private ExtendedVFS eVFS;

        private Result currentResult = null;
        private Action currentAction = null;

        /// <summary>
        /// A result which is false - no need to create a false result every time when needed a false result
        /// </summary>
        public readonly Result defaultResult = new Result(false, false);

        /// <summary>
        /// This event is called when the method is finishing the progress
        /// </summary>
        /// <param name="result">The result information from the method</param>
        public delegate void onFinishedEvent(Result result);

        /// <summary>
        /// This event is called when the method is finishing the progress (Be careful with calling another method, because it will be called every time when onFinished is fired)
        /// </summary>
        public event onFinishedEvent OnFinished;
                
        /// <summary>
        /// Returns the StopWatch-Instance of the current process
        /// </summary>
        public Stopwatch CurrentStopWatch => this.stw;

        /// <summary>
        /// If true, creates the file completly new after a change like calling WriteAllText or WriteAllBytes
        /// </summary>
        public bool SaveAfterChange => eVFS.SaveAfterChange;

        /// <summary>
        /// Root Directory - Name: ""
        /// </summary>
        public override IDirectory RootDirectory => eVFS.RootDirectory;

        /// <summary>
        /// A File which doesn't relay to somenthing, just to use some methods which aren't static anymore (Since IFile and IDirectory-Interfaces)
        /// </summary>
        public override IFile NULLFILE => eVFS.NULLFILE;

        /// <summary>
        /// Instantiate a new VFS which is modified and thread-based
        /// </summary>
        /// <param name="savePath">The path where the file will be created</param>
        /// <param name="workSpacePath">The path of the cliboard (Please do not save an files or directories there!)</param>
        /// <param name="saveAfterChanged">Determines if the system should save after a change</param>
        /// <param name="BufferSize">The size of the file-buffer in bytes</param>
        public ExtendedVFST(string savePath, string workSpacePath, bool saveAfterChanged = true, long BufferSize = 32768)
        {
            this.savePath = savePath;
            this.eVFS = new ExtendedVFS(savePath, workSpacePath, BufferSize);
            this.eVFS.SaveAfterChange = saveAfterChanged;

            this.currentWorker = new BackgroundWorker();
            this.currentWorker.DoWork += CurrentWorker_DoWork;
            this.currentWorker.RunWorkerCompleted += CurrentWorker_RunWorkerCompleted;
        }   

        #region Private
        private void endStopWatch()
        {
            if (this.stw != null)
            {
                this.stw.Stop();
                this.stw = null;
            }
        }

        private void onFinished(Result result)
        {
            this.isBusy = false;
            this.result = result;

            this.OnFinished?.Invoke(result);
        }

        private bool changeBusy()
        {
            if (this.isBusy)
            {
                this.OnFinished?.Invoke(new Result(false));
                return false;
            }

            this.isBusy = true;
            return true;
        }

        private void CurrentWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.onFinished(this.currentResult);
        }

        private void CurrentWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            this.currentAction.Invoke();
        }

        #endregion

        /// <summary>
        /// Cancels the current thread
        /// </summary>
        /// <param name="currentAction">The method which is working currently</param>
        public void CancelThread(Methods currentAction)
        {
            this.currentWorker.CancelAsync();
            this.onFinished(new Result(false, currentAction));
        }

        /// <summary>
        /// Creates a new VHP
        /// </summary>
        /// <param name="directory">All files and directories of this directory will be progressed into the VHP</param>
        /// <returns></returns>
        public override void Create(string directory)
        {
            if (!this.changeBusy())
                return;

            this.currentAction = new Action(() =>
            {
                stw = Stopwatch.StartNew();
                try
                {
                    eVFS.Create(directory);
                    this.currentResult = new Result(true, Methods.CREATE);
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.CREATE);
                }
                this.endStopWatch();
            });

            this.currentWorker.RunWorkerAsync();
        }


        /// <summary>
        /// Creates a new VHP
        /// </summary>
        /// <param name="directories">This directories will be taken to be progressed</param>
        /// <param name="files">This files will be taken to be progressed</param>
        /// <returns></returns>
        public override void Create(string[] directories, string[] files)
        {
            if (!this.changeBusy())
                return;

            this.currentAction = delegate {
                stw = Stopwatch.StartNew();
                try
                {
                    eVFS.Create(directories, files);
                    this.currentResult = new Result(true, Methods.CREATE);                   
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.CREATE);
                }
                this.endStopWatch();
            };

            this.currentWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Extract all files and directories to the given path
        /// </summary>
        /// <param name="filePath">Path where the content will be extracted</param>
        /// <returns></returns>
        public override bool Extract(string filePath)
        {
            if (!this.changeBusy())
                return false;

            this.currentAction = delegate
            {
                stw = Stopwatch.StartNew();
                try
                {
                    bool result = eVFS.Extract(filePath);
                    this.currentResult = new Result(result, Methods.EXTRACT);
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.EXTRACT);
                }
                this.endStopWatch();
            };

            currentWorker.RunWorkerAsync();
            return true;
        }

        /// <summary>
        /// Extract the directory to a given path
        /// </summary>
        /// <param name="path">The virutal path where the directory is stored</param>
        /// <param name="filePath">Path where the directory will be extracted</param>
        public override void ExtractDirectory(string path, string filePath)
        {
            if (!this.changeBusy())
                return;

            this.currentAction = delegate
            {
                stw = Stopwatch.StartNew();
                try
                {
                    eVFS.ExtractDirectory(path, filePath);
                    this.currentResult = new Result(true, Methods.EXTRACT_DIR);
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.EXTRACT_DIR);
                }
                this.endStopWatch();
            };

            currentWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Extracts the given directory to the given path
        /// </summary>
        /// <param name="currentDir">The virutal directory which should be extracted</param>
        /// <param name="toPath">Path where the virtual directory will be extracted</param>
        public override void ExtractDirectory(IDirectory currentDir, string toPath)
        {
            if (!this.changeBusy())
                return;

            this.currentAction = delegate
            {
                stw = Stopwatch.StartNew();
                try
                {
                    eVFS.ExtractDirectory(currentDir, toPath);
                    this.currentResult = new Result(true, Methods.EXTRACT_DIR);
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.EXTRACT_DIR);
                }
                this.endStopWatch();
            };

            currentWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Extracts the given files to the given path
        /// </summary>
        /// <param name="files">The files which should be extracted</param>
        /// <param name="directoryPath">Path where the files will be extracted</param>
        public override void ExtractFiles(IFile[] files, string directoryPath)
        {
            if (!this.changeBusy())
                return;

            this.currentAction = delegate
            {
                stw = Stopwatch.StartNew();
                try
                {
                    eVFS.ExtractFiles(files, directoryPath);
                    this.currentResult = new Result(true, Methods.EXTRACT_FILES);
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.EXTRACT_FILES);
                }
                this.endStopWatch();
            };

            currentWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Extracts the given files to the given path
        /// </summary>
        /// <param name="files">The files which should be extracted</param>
        /// <param name="directoryPath">Path where the files will be extracted</param>
        public override void ExtractFiles(string[] files, string directoryPath)
        {
            if (!this.changeBusy())
                return;

            this.currentAction = delegate
            {
                stw = Stopwatch.StartNew();
                try
                {
                    eVFS.ExtractFiles(files, directoryPath);
                    this.currentResult = new Result(true, Methods.EXTRACT_FILES);
                    this.endStopWatch();
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.EXTRACT_FILES);
                }
            };

            currentWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Returns true if a file is already existing
        /// </summary>
        /// <param name="path">Path of the virtual file</param>
        /// <param name="startNode">The directory where the path is beginning</param>
        /// <returns></returns>
        public override bool FileExists(string path, IDirectory startNode)
        {
            // No thread based needed for this method. Overriding is just to be sure
            return base.FileExists(path, startNode);
        }

        /// <summary>
        /// Removes a virtual file
        /// </summary>
        /// <param name="path">Path of the virtual file</param>
        /// <param name="startNode">The directory where the path is beginning</param>
        /// <returns></returns>
        public override bool RemoveFile(string path, IDirectory startNode)
        {
            // No thread based needed for this method.
            // It just deletes the file out of the list, but if you save then thread based.
            if (!eVFS.SaveAfterChange)
                return eVFS.RemoveFile(path, startNode);
            else
            {
                // Thread based
                if (!this.changeBusy())
                    return false;

                this.currentAction = delegate
                {
                    stw = Stopwatch.StartNew();
                    try
                    {
                        bool result = eVFS.RemoveFile(path, startNode);
                        this.currentResult = new Result(result, Methods.REMOVE_FILE);
                    }
                    catch (Exception e)
                    {
                        this.currentResult = new Result(false, e, Methods.REMOVE_FILE);
                    }
                    this.endStopWatch();
                };

                currentWorker.RunWorkerAsync();
                return true;
            }
        }

        /// <summary>
        /// Loads a VHP-File into the RAM (just header-content)
        /// </summary>
        /// <param name="filePath">The path where the vhp-file is stored</param>
        public override void Read(string filePath)
        {
            if (!this.changeBusy())
                return;

            this.currentAction = delegate
            {
                stw = Stopwatch.StartNew();
                try
                {
                    eVFS.Read(filePath);
                    this.currentResult = new Result(true, Methods.READ);
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.READ);
                }
                this.endStopWatch();
            };

            currentWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Returns the bytes of a virutal files (reading from originial file) (Max: 1 GB)
        /// </summary>
        /// <param name="path">Path of the virtual file</param>
        /// <param name="startNode">The directory where the path is beginning</param>
        /// <param name="different">Just to differniate between these methods (not used in this method)</param>
        /// <returns></returns>
        public override byte[] ReadAllBytes(string path, IDirectory startNode, bool different = false)
        {
            if (!this.changeBusy())
                return null;

            this.currentAction = delegate
            {
                stw = Stopwatch.StartNew();
                try
                {
                    byte[] temp = eVFS.ReadAllBytes(path, startNode, different);
                    this.currentResult = new Result(true, temp, Methods.READ_ALL_BYTES);
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.READ_ALL_BYTES);
                }
                this.endStopWatch();
            };

            currentWorker.RunWorkerAsync();
            return null;
        }

        /// <summary>
        /// Returns the content of a virtual file as a string (Max: 1 GB)
        /// </summary>
        /// <param name="path">Path of the virtual file</param>
        /// <param name="startNode">The directory where the path is beginning</param>
        /// <returns></returns>
        public override string ReadAllText(string path, IDirectory startNode)
        {
            if (!this.changeBusy())
                return null;

            this.currentAction = delegate
            {
                stw = Stopwatch.StartNew();
                try
                {
                    string temp = eVFS.ReadAllText(path, startNode);
                    this.currentResult = new Result(true, temp, Methods.READ_ALL_TEXT);
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.READ_ALL_TEXT);
                }
                this.endStopWatch();
            };

            currentWorker.RunWorkerAsync();
            return null;
        }

        /// <summary>
        /// Writes a stream (file) into a file in the workspace directory (while saving the file will be saved too)
        /// </summary>
        /// <param name="name">The name of the file</param>
        /// <param name="dir">The directory where the file is stored in</param>
        /// <param name="stream">The stream which will be written to a file stream</param>
        /// <param name="overrideExisting">Determines if the file will be replace if the file is already exisiting</param>
        /// <returns></returns>
        public override bool WriteStream(string name, IDirectory dir, Stream stream, bool overrideExisting = false)
        {
            if (!this.changeBusy())
                return false;

            this.currentAction = delegate
            {
                stw = Stopwatch.StartNew();
                try
                {
                    bool result = eVFS.WriteStream(name, dir, stream, overrideExisting);
                    this.currentResult = new Result(result, Methods.WRITE_STREAM);
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.WRITE_STREAM);
                }
                this.endStopWatch();
            };

            currentWorker.RunWorkerAsync();
            return true;
        }

        /// <summary>
        /// Writes bytes into a file in the workspace directory (while saving the file will be saved too)
        /// </summary>
        /// <param name="data">The bytes to write</param>
        /// <param name="name">The name of the file</param>
        /// <param name="dir">The directory where the file is stored in</param>
        /// <param name="overrideExisting">Determines if the file will be replace if the file is already exisiting</param>
        /// <returns></returns>
        public override bool WriteAllBytes(byte[] data, string name, IDirectory dir, bool overrideExisting = false)
        {
            if (!this.changeBusy())
                return false;

            this.currentAction = delegate
            {
                stw = Stopwatch.StartNew();
                try
                {
                    bool result = eVFS.WriteAllBytes(data, name, dir, overrideExisting);
                    this.currentResult = new Result(result, Methods.WRITE_ALL_BYTES);
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.WRITE_ALL_BYTES);
                }
                this.endStopWatch();
            };

            this.currentWorker.RunWorkerAsync();
            return true;
        }

        /// <summary>
        /// Writes bytes into a file in the workspace directory (while saving the file will be saved too)
        /// </summary>
        /// <param name="data">The bytes to write</param>
        /// <param name="path">The path where the file is stored in</param>
        /// <param name="overrideExisting">Determines if the file will be replace if the file is already exisiting</param>
        /// <returns></returns>
        public override bool WriteAllBytes(byte[] data, string path, bool overrideExisting = false)
        {
            if (!this.changeBusy())
                return false;

            this.currentAction = delegate
            {
                stw = Stopwatch.StartNew();
                try
                {
                    bool result = eVFS.WriteAllBytes(data, path, overrideExisting);
                    this.currentResult = new Result(result, Methods.WRITE_ALL_BYTES);
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.WRITE_ALL_BYTES);
                }
                this.endStopWatch();
            };

            this.currentWorker.RunWorkerAsync();
            return true;
        }

        /// <summary>
        /// Writes bytes into a file in the workspace directory (while saving the file will be saved too)
        /// </summary>
        /// <param name="content">The string which should be written into the file</param>
        /// <param name="name">The name of the file</param>
        /// <param name="dir">The directory where the file is stored in</param>
        /// <param name="overrideExisting">Determines if the file will be replace if the file is already exisiting</param>
        /// <returns></returns>
        public override bool WriteAllText(string content, string name, IDirectory dir, bool overrideExisting = false)
        {
            if (!this.changeBusy())
                return false;

            this.currentAction = delegate
            {
                stw = Stopwatch.StartNew();
                try
                {
                    bool result = eVFS.WriteAllText(content, name, dir, overrideExisting);
                    this.currentResult = new Result(result, Methods.WRITE_ALL_TEXT);
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.WRITE_ALL_TEXT);
                }
                this.endStopWatch();
            };

            this.currentWorker.RunWorkerAsync();
            return true;
        }

        /// <summary>
        /// Automatically called when SaveAfterChange is true.
        /// This method is for saving changes.
        /// </summary>
        /// <returns></returns>
        public override bool Save()
        {
            if (!this.changeBusy())
                return false;

            this.currentAction = delegate
            {
                stw = Stopwatch.StartNew();
                try
                {
                    bool result = eVFS.Save();
                    this.currentResult = new Result(result, Methods.SAVE);
                }
                catch (Exception e)
                {
                    this.currentResult = new Result(false, e, Methods.SAVE);
                }
                this.endStopWatch();
            };

            this.currentWorker.RunWorkerAsync();
            return true;
        }
    }
}
