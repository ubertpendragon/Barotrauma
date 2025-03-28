﻿#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
#if CLIENT
using Barotrauma.Networking;
using Barotrauma.Steam;
#endif

namespace Barotrauma.IO
{
    static class Validation
    {
        private static readonly ImmutableArray<Identifier> unwritableDirs = new[] { "Content".ToIdentifier() }.ToImmutableArray();
        private static readonly ImmutableArray<Identifier> unwritableExtensions = new[]
        {
            ".exe", ".dll", ".json", ".pdb", ".com", ".scr", ".dylib", ".so", ".a", ".app", //executables and libraries
            ".bat", ".sh", //shell scripts
        }.ToIdentifiers().ToImmutableArray();

        public ref struct Skipper
        {
            public void Dispose()
            {
                SkipValidationInDebugBuilds = false;
            }
        }

        /// <summary>
        /// Skips validation for as long as the returned object remains in scope (remember to use using)
        /// </summary>
        public static Skipper SkipInDebugBuilds()
        {
            SkipValidationInDebugBuilds = true;
            return new Skipper();
        }

        /// <summary>
        /// When set to true, the game is allowed to modify the vanilla content in debug builds. Has no effect in non-debug builds.
        /// </summary>
        public static bool SkipValidationInDebugBuilds;

        public static bool CanWrite(string path, bool isDirectory)
        {
            string getFullPath(string p)
                => System.IO.Path.GetFullPath(p).CleanUpPath();
            
            path = getFullPath(path);
            string localModsDir = getFullPath(ContentPackage.LocalModsDir);
            string workshopModsDir = getFullPath(ContentPackage.WorkshopModsDir);
#if CLIENT
            string workshopStagingDir = getFullPath(SteamManager.Workshop.PublishStagingDir);
            string tempDownloadDir = getFullPath(ModReceiver.DownloadFolder);
#endif

            if (!isDirectory)
            {
                Identifier extension = System.IO.Path.GetExtension(path).Replace(" ", "").ToIdentifier();

                bool pathStartsWith(string prefix)
                    => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                
                if (!pathStartsWith(workshopModsDir)
                    && !pathStartsWith(localModsDir)
#if CLIENT
                    && !pathStartsWith(tempDownloadDir)
                    && !pathStartsWith(workshopStagingDir)
#endif
                    && unwritableExtensions.Any(e => e == extension))
                {
                    return false;
                }
            }
            
            foreach (var unwritableDir in unwritableDirs)
            {
                string dir = System.IO.Path.GetFullPath(unwritableDir.Value).CleanUpPath();

                if (path.StartsWith(dir, StringComparison.InvariantCultureIgnoreCase))
                {
#if DEBUG
                    return SkipValidationInDebugBuilds;
#else
                    return false;
#endif
                }
            }

            return true;
        }
    }

    public static class SafeXML
    {
        public static void SaveSafe(
            this System.Xml.Linq.XDocument doc,
            string path,
            System.Xml.Linq.SaveOptions saveOptions = System.Xml.Linq.SaveOptions.None,
            bool throwExceptions = false)
        {
            if (!Validation.CanWrite(path, false))
            {
                string errorMsg = $"Cannot save XML document to \"{path}\": modifying the files in this folder/with this extension is not allowed.";
                if (throwExceptions)
                {
                    throw new InvalidOperationException(errorMsg);
                }
                else
                {
                    DebugConsole.ThrowError(errorMsg);
                }
                return;
            }
            doc.Save(path, saveOptions);
        }

        public static void SaveSafe(this System.Xml.Linq.XElement element, string path, bool throwExceptions = false)
        {
            if (!Validation.CanWrite(path, false))
            {
                string errorMsg = $"Cannot save XML element to \"{path}\": modifying the files in this folder/with this extension is not allowed.";
                if (throwExceptions)
                {
                    throw new InvalidOperationException(errorMsg);
                }
                else
                {
                    DebugConsole.ThrowError(errorMsg);
                }
                return;
            }
            element.Save(path);
        }

        public static void SaveSafe(this System.Xml.Linq.XDocument doc, XmlWriter writer)
        {
            doc.WriteTo(writer);
        }

        public static void WriteTo(this System.Xml.Linq.XDocument doc, XmlWriter writer)
        {
            writer.Write(doc);
        }
    }

    public class XmlWriter : IDisposable
    {
        public readonly System.Xml.XmlWriter? Writer;

        public XmlWriter(string path, System.Xml.XmlWriterSettings settings)
        {
            if (!Validation.CanWrite(path, false))
            {
                DebugConsole.ThrowError($"Cannot write XML document to \"{path}\": modifying the files in this folder/with this extension is not allowed.");
                Writer = null;
                return;
            }
            Writer = System.Xml.XmlWriter.Create(path, settings);
        }

        public static XmlWriter Create(string path, System.Xml.XmlWriterSettings settings)
        {
            return new XmlWriter(path, settings);
        }

        public void Write(System.Xml.Linq.XDocument doc)
        {
            if (Writer == null)
            {
                DebugConsole.ThrowError("Cannot write to invalid XmlWriter");
                return;
            }
            doc.WriteTo(Writer);
        }

        public void Flush()
        {
            if (Writer == null)
            {
                DebugConsole.ThrowError("Cannot flush invalid XmlWriter");
                return;
            }
            Writer.Flush();
        }

        public void Dispose()
        {
            if (Writer == null)
            {
                DebugConsole.ThrowError("Cannot dispose invalid XmlWriter");
                return;
            }
            Writer.Dispose();
        }
    }

    public static class XmlWriterExtensions
    {
        public static void Save(this System.Xml.Linq.XDocument doc, XmlWriter writer)
        {
            doc.Save(writer.Writer ?? throw new NullReferenceException("Unable to save XML document: XML writer is null."));
        }
    }

    public static class Path
    {
        public static readonly char DirectorySeparatorChar = System.IO.Path.DirectorySeparatorChar;
        public static readonly char AltDirectorySeparatorChar = System.IO.Path.AltDirectorySeparatorChar;

        public static string GetExtension(string path) => System.IO.Path.GetExtension(path);

        public static string GetFileNameWithoutExtension(string path) => System.IO.Path.GetFileNameWithoutExtension(path);

        public static string? GetPathRoot(string? path) => System.IO.Path.GetPathRoot(path);

        public static string GetRelativePath(string relativeTo, string path) => System.IO.Path.GetRelativePath(relativeTo, path);

        public static string GetDirectoryName(ContentPath path) => GetDirectoryName(path.Value)!;
        
        public static string? GetDirectoryName(string path) => System.IO.Path.GetDirectoryName(path);

        public static string GetFileName(string path) => System.IO.Path.GetFileName(path);

        public static string GetFullPath(string path) => System.IO.Path.GetFullPath(path);

        public static string Combine(params string[] s) => System.IO.Path.Combine(s);

        public static string GetTempFileName() => System.IO.Path.GetTempFileName();

        public static bool IsPathRooted(string path) => System.IO.Path.IsPathRooted(path);

        private static readonly ImmutableHashSet<char> invalidFileNameChars = ImmutableHashSet.Create
        (
            '\"', '<', '>', '|', '\0',
            (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
            (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
            (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
            (char)31, ':', '*', '?', '\\', '/'
        );

        /// <summary>
        /// Returns file name characters that are invalid on any of our supported platforms (essentially the list of invalid characters on Windows)
        /// </summary>
        public static ImmutableHashSet<char> GetInvalidFileNameCharsCrossPlatform() => invalidFileNameChars;
    }

    public static class Directory
    {
        public static string GetCurrentDirectory()
        {
            // Intentionally crash with all exceptions, if this fails.
            return System.IO.Directory.GetCurrentDirectory();
        }

        public static void SetCurrentDirectory(string path)
        { 
            // Intentionally crash with all exceptions, if this fails.
            System.IO.Directory.SetCurrentDirectory(path);
        }
        
        private static readonly EnumerationOptions IgnoreInaccessibleSystemAndHidden = new EnumerationOptions
        {
            MatchType = MatchType.Win32,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden,
            IgnoreInaccessible = true
        };
        
        private static EnumerationOptions GetEnumerationOptions(bool ignoreInaccessible, bool recursive)
        {
            return new EnumerationOptions
            {
                MatchType = MatchType.Win32,
                AttributesToSkip = FileAttributes.System | FileAttributes.Hidden,
                IgnoreInaccessible = ignoreInaccessible,
                RecurseSubdirectories = recursive
            };
        }

        public static string[] GetFiles(string path)
        {
            return System.IO.Directory.GetFiles(path, "*", IgnoreInaccessibleSystemAndHidden);
        }

        public static string[] GetFiles(string path, string pattern, SearchOption option = SearchOption.AllDirectories)
        {
            EnumerationOptions enumerationOptions = GetEnumerationOptions(ignoreInaccessible: true, option == SearchOption.AllDirectories);
            return System.IO.Directory.GetFiles(path, pattern, enumerationOptions);
        }

        public static string[] GetDirectories(string path, string searchPattern = "*")
        {
            return System.IO.Directory.GetDirectories(path, searchPattern, IgnoreInaccessibleSystemAndHidden);
        }

        public static string[] GetFileSystemEntries(string path)
        {
            return System.IO.Directory.GetFileSystemEntries(path, "*", IgnoreInaccessibleSystemAndHidden);
        }

        public static IEnumerable<string> EnumerateDirectories(string path, string pattern)
        { 
            return System.IO.Directory.EnumerateDirectories(path, pattern, IgnoreInaccessibleSystemAndHidden);
        }

        public static IEnumerable<string> EnumerateFiles(string path, string pattern)
        { 
            return System.IO.Directory.EnumerateFiles(path, pattern, IgnoreInaccessibleSystemAndHidden);
        }

        public static bool Exists(string path)
        {
            return System.IO.Directory.Exists(path);
        }

        public static System.IO.DirectoryInfo? CreateDirectory(string path, bool catchUnauthorizedAccessExceptions = false)
        {
            if (!Validation.CanWrite(path, true))
            {
                DebugConsole.ThrowError($"Cannot create directory \"{path}\": modifying the contents of this folder/using this extension is not allowed.");
                Validation.CanWrite(path, true);
                return null;
            }
            try
            {
                return System.IO.Directory.CreateDirectory(path);
            }
            catch (UnauthorizedAccessException e)
            {
                DebugConsole.ThrowError($"Cannot create directory at \"{path}\": unauthorized access. The file/folder might be read-only!", e);
                if (!catchUnauthorizedAccessExceptions) { throw; }
                return null;
            }
        }

        public static void Delete(string path, bool recursive = true, bool catchUnauthorizedAccessExceptions = true)
        {
            if (!Validation.CanWrite(path, true))
            {
                DebugConsole.ThrowError($"Cannot delete directory \"{path}\": modifying the contents of this folder/using this extension is not allowed.");
                return;
            }
            //TODO: validate recursion?
            try
            {
                System.IO.Directory.Delete(path, recursive);
            }
            catch (UnauthorizedAccessException e)
            {
                DebugConsole.ThrowError($"Cannot delete \"{path}\": unauthorized access. The file/folder might be read-only!", e);
                if (!catchUnauthorizedAccessExceptions) { throw; }
            }
        }

        public static bool TryDelete(string path, bool recursive = true)
        {
            try
            {
                Delete(path, recursive, catchUnauthorizedAccessExceptions: false);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public static DateTime GetLastWriteTime(string path, bool catchUnauthorizedAccessExceptions = true)
        {
            try
            {
                return System.IO.Directory.GetLastWriteTime(path);
            }
            catch (UnauthorizedAccessException e)
            {
                DebugConsole.ThrowError($"Cannot get last write time at \"{path}\": unauthorized access. The file/folder might be read-only!", e);
                if (!catchUnauthorizedAccessExceptions) { throw; }
                return new DateTime();
            }
        }

        public static void Copy(string src, string dest, bool overwrite = false)
        {
            if (!Validation.CanWrite(dest, true))
            {
                DebugConsole.ThrowError($"Cannot copy \"{src}\" to \"{dest}\": modifying the contents of the destination folder is not allowed.");
                return;
            }

            CreateDirectory(dest);

            foreach (string path in GetFiles(src))
            {
                File.Copy(path, Path.Combine(dest, Path.GetRelativePath(src, path)), overwrite);
            }

            foreach (string path in GetDirectories(src))
            {
                Copy(path, Path.Combine(dest, Path.GetRelativePath(src, path)), overwrite);
            }
        }

        public static void Move(string src, string dest, bool overwrite = false)
        {
            if (!overwrite && Exists(dest))
            {
                DebugConsole.ThrowError($"Cannot move \"{src}\" to \"{dest}\": destination folder already exists.");
                return;
            }

            if (!Validation.CanWrite(src, true))
            {
                DebugConsole.ThrowError($"Cannot move \"{src}\" to \"{dest}\": modifying the contents of the source folder is not allowed.");
                return;
            }

            if (!Validation.CanWrite(dest, true))
            {
                DebugConsole.ThrowError($"Cannot move \"{src}\" to \"{dest}\": modifying the contents of the destination folder is not allowed.");
                return;
            }

            if (!overwrite || !Exists(dest) || TryDelete(dest))
            {
                System.IO.Directory.Move(src, dest);
            }
        }
    }

    public static class File
    {
        public static bool Exists(ContentPath path) => Exists(path.Value);
        
        public static bool Exists(string path) => System.IO.File.Exists(path);

        public static void Copy(string src, string dest, bool overwrite = false, bool catchUnauthorizedAccessExceptions = true)
        {
            if (!Validation.CanWrite(dest, false))
            {
                DebugConsole.ThrowError($"Cannot copy \"{src}\" to \"{dest}\": modifying the contents of this folder/using this extension is not allowed.");
                return;
            }
            try
            {
                System.IO.File.Copy(src, dest, overwrite);   
            }
            catch (UnauthorizedAccessException e)
            {
                DebugConsole.ThrowError($"Cannot copy \"{src}\" to \"{dest}\": unauthorized access. The file/folder might be read-only!", e);
                if (!catchUnauthorizedAccessExceptions) { throw; }
            }
        }

        public static void Move(string src, string dest, bool catchUnauthorizedAccessExceptions = true)
        {
            if (!Validation.CanWrite(src, false))
            {
                DebugConsole.ThrowError($"Cannot move \"{src}\" to \"{dest}\": modifying the contents of the source folder is not allowed.");
                return;
            }
            if (!Validation.CanWrite(dest, false))
            {
                DebugConsole.ThrowError($"Cannot move \"{src}\" to \"{dest}\": modifying the contents of the destination folder is not allowed");
                return;
            }
            try
            {
                System.IO.File.Move(src, dest);
            }
            catch (UnauthorizedAccessException e)
            {
                DebugConsole.ThrowError($"Cannot move \"{src}\" to \"{dest}\": unauthorized access. The file/folder might be read-only!", e);
                if (!catchUnauthorizedAccessExceptions) { throw; }
            }
        }

        public static void Delete(ContentPath path, bool catchUnauthorizedAccessExceptions = true) => Delete(path.Value, catchUnauthorizedAccessExceptions);
        
        public static void Delete(string path, bool catchUnauthorizedAccessExceptions = true)
        {
            if (!Validation.CanWrite(path, false))
            {
                DebugConsole.ThrowError($"Cannot delete file \"{path}\": modifying the contents of this folder/using this extension is not allowed.");
                return;
            }
            try
            {
                System.IO.File.Delete(path);
            }
            catch (UnauthorizedAccessException e)
            {
                DebugConsole.ThrowError($"Cannot delete {path}: unauthorized access. The file/folder might be read-only!", e);
                if (!catchUnauthorizedAccessExceptions) { throw; }
            }
        }

        public static DateTime GetLastWriteTime(string path)
        {
            return System.IO.File.GetLastWriteTime(path);
        }

        public static FileStream? Open(
            string path,
            System.IO.FileMode mode,
            System.IO.FileAccess access = System.IO.FileAccess.ReadWrite,
            System.IO.FileShare? share = null,
            bool catchUnauthorizedAccessExceptions = true)
        {
            switch (mode)
            {
                case System.IO.FileMode.Create:
                case System.IO.FileMode.CreateNew:
                case System.IO.FileMode.OpenOrCreate:
                case System.IO.FileMode.Append:
                case System.IO.FileMode.Truncate:
                    if (!Validation.CanWrite(path, false))
                    {
                        DebugConsole.ThrowError($"Cannot open \"{path}\" in {mode} mode: modifying the contents of this folder/using this extension is not allowed.");
                        return null;
                    }
                    break;
            }
            access =
                !Validation.CanWrite(path, false) ?
                System.IO.FileAccess.Read :
                access;
            var shareVal = share ?? (access == System.IO.FileAccess.Read ? System.IO.FileShare.Read : System.IO.FileShare.None);
            try
            {
                return new FileStream(path, System.IO.File.Open(path, mode, access, shareVal));
            }
            catch (UnauthorizedAccessException e)
            {
                DebugConsole.ThrowError($"Cannot open {path} (stream): unauthorized access. The file/folder might be read-only!", e);
                if (!catchUnauthorizedAccessExceptions) { throw; }
                return null;
            }
        }

        public static FileStream? OpenRead(string path, bool catchUnauthorizedAccessExceptions = true)
        {
            return Open(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, catchUnauthorizedAccessExceptions: catchUnauthorizedAccessExceptions);
        }

        public static FileStream? OpenWrite(string path, bool catchUnauthorizedAccessExceptions = true)
        {
            return Open(path, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write, catchUnauthorizedAccessExceptions: catchUnauthorizedAccessExceptions);
        }

        public static FileStream? Create(string path, bool catchUnauthorizedAccessExceptions = true)
        {
            return Open(path, System.IO.FileMode.Create, System.IO.FileAccess.Write, catchUnauthorizedAccessExceptions: catchUnauthorizedAccessExceptions);
        }

        public static void WriteAllBytes(string path, byte[] contents, bool catchUnauthorizedAccessExceptions = true)
        {
            if (!Validation.CanWrite(path, false))
            {
                DebugConsole.ThrowError($"Cannot write all bytes to \"{path}\": modifying the files in this folder/with this extension is not allowed.");
                return;
            }
            try
            {
                System.IO.File.WriteAllBytes(path, contents);
            }
            catch (UnauthorizedAccessException e)
            {
                DebugConsole.ThrowError($"Cannot write at {path}: unauthorized access. The file/folder might be read-only!", e);
                if (!catchUnauthorizedAccessExceptions) { throw; }
            }
        }

        public static void WriteAllText(string path, string contents, System.Text.Encoding? encoding = null, bool catchUnauthorizedAccessExceptions = true)
        {
            if (!Validation.CanWrite(path, false))
            {
                DebugConsole.ThrowError($"Cannot write all text to \"{path}\": modifying the files in this folder/with this extension is not allowed.");
                return;
            }
            try
            {
                System.IO.File.WriteAllText(path, contents, encoding ?? System.Text.Encoding.UTF8);
            }
            catch (UnauthorizedAccessException e)
            {
                DebugConsole.ThrowError($"Cannot write at {path}: unauthorized access. The file/folder might be read-only!", e);
                if (!catchUnauthorizedAccessExceptions) { throw; }
            }
        }

        public static void WriteAllLines(string path, IEnumerable<string> contents, System.Text.Encoding? encoding = null, bool catchUnauthorizedAccessExceptions = true)
        {
            if (!Validation.CanWrite(path, false))
            {
                DebugConsole.ThrowError($"Cannot write all lines to \"{path}\": modifying the files in this folder/with this extension is not allowed.");
                return;
            }
            try
            {
                System.IO.File.WriteAllLines(path, contents, encoding ?? System.Text.Encoding.UTF8);
            }
            catch (UnauthorizedAccessException e)
            {
                DebugConsole.ThrowError($"Cannot write at {path}: unauthorized access. The file/folder might be read-only!", e);
                if (!catchUnauthorizedAccessExceptions) { throw; }
            }
        }

        public static byte[] ReadAllBytes(string path, bool catchUnauthorizedAccessExceptions = true)
        {
            try
            {
                return System.IO.File.ReadAllBytes(path);
            }
            catch (UnauthorizedAccessException e)
            {
                DebugConsole.ThrowError($"Cannot read {path}: unauthorized access. The file/folder might be read-only!", e);
                if (!catchUnauthorizedAccessExceptions) { throw; }
                return Array.Empty<byte>();
            }
        }

        public static string ReadAllText(string path, System.Text.Encoding? encoding = null, bool catchUnauthorizedAccessExceptions = true)
        {
            try
            {
                return System.IO.File.ReadAllText(path, encoding ?? System.Text.Encoding.UTF8);
            }
            catch (UnauthorizedAccessException e)
            {
                DebugConsole.ThrowError($"Cannot read {path}: unauthorized access. The file/folder might be read-only!", e);
                if (!catchUnauthorizedAccessExceptions) { throw; }
                return string.Empty;
            }
        }

        public static string[] ReadAllLines(string path, System.Text.Encoding? encoding = null, bool catchUnauthorizedAccessExceptions = true)
        {
            try
            {
                return System.IO.File.ReadAllLines(path, encoding ?? System.Text.Encoding.UTF8);
            }
            catch (UnauthorizedAccessException e)
            {
                DebugConsole.ThrowError($"Cannot read {path}: unauthorized access. The file/folder might be read-only!", e);
                if (!catchUnauthorizedAccessExceptions) { throw; }
                return Array.Empty<string>();
            }
        }
        public static string SanitizeName(string str)
        {
            string sanitized = "";
            foreach (char c in str)
            {
                char newChar = Path.GetInvalidFileNameCharsCrossPlatform().Contains(c) ? '-' : c;
                sanitized += newChar;
            }
            return sanitized;
        }
    }

    public class FileStream : System.IO.Stream
    {
        private readonly System.IO.FileStream innerStream;
        private readonly string fileName;

        public FileStream(string fn, System.IO.FileStream stream)
        {
            innerStream = stream;
            fileName = fn;
        }

        public override bool CanRead => innerStream.CanRead;
        public override bool CanSeek => innerStream.CanSeek;
        public override bool CanTimeout => innerStream.CanTimeout;
        public override bool CanWrite
        {
            get
            {
                if (!Validation.CanWrite(fileName, false)) { return false; }
                return innerStream.CanWrite;
            }
        }

        public override long Length => innerStream.Length;

        public override long Position
        {
            get
            {
                return innerStream.Position;
            }
            set
            {
                innerStream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return innerStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Validation.CanWrite(fileName, false))
            {
                innerStream.Write(buffer, offset, count);
            }
            else
            {
                DebugConsole.ThrowError($"Cannot write to file \"{fileName}\": modifying the files in this folder/with this extension is not allowed.");
            }
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            return innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            innerStream.SetLength(value);
        }

        public override void Flush()
        {
            innerStream.Flush();
        }

        protected override void Dispose(bool notCalledByFinalizer)
        {
            if (notCalledByFinalizer) { innerStream.Dispose(); }
        }
    }

    public class DirectoryInfo
    {
        private System.IO.DirectoryInfo innerInfo;

        public DirectoryInfo(string path)
        {
            innerInfo = new System.IO.DirectoryInfo(path);
        }

        private DirectoryInfo(System.IO.DirectoryInfo info)
        {
            innerInfo = info;
        }

        public bool Exists => innerInfo.Exists;
        public string Name => innerInfo.Name;
        public string FullName => innerInfo.FullName;

        public System.IO.FileAttributes Attributes => innerInfo.Attributes;

        public IEnumerable<DirectoryInfo> GetDirectories()
        {
            var dirs = innerInfo.GetDirectories();
            foreach (var dir in dirs)
            {
                yield return new DirectoryInfo(dir);
            }
        }

        public IEnumerable<FileInfo> GetFiles()
        {
            var files = innerInfo.GetFiles();
            foreach (var file in files)
            {
                yield return new FileInfo(file);
            }
        }

        public void Delete()
        {
            if (!Validation.CanWrite(innerInfo.FullName, false))
            {
                DebugConsole.ThrowError($"Cannot delete directory \"{Name}\": modifying the contents of this folder/using this extension is not allowed.");
                return;
            }
            innerInfo.Delete();
        }
    }

    public class FileInfo
    {
        private System.IO.FileInfo innerInfo;

        public FileInfo(string path)
        {
            innerInfo = new System.IO.FileInfo(path);
        }

        public FileInfo(System.IO.FileInfo info)
        {
            innerInfo = info;
        }

        public bool Exists => innerInfo.Exists;
        public string Name => innerInfo.Name;
        public string FullName => innerInfo.FullName;
        public long Length => innerInfo.Length;

        public bool IsReadOnly
        {
            get
            {
                return innerInfo.IsReadOnly;
            }
            set
            {
                if (!Validation.CanWrite(innerInfo.FullName, false))
                {
                    DebugConsole.ThrowError($"Cannot set read-only to {value} for \"{Name}\": modifying the files in this folder/with this extension is not allowed.");
                    return;
                }
                innerInfo.IsReadOnly = value;
            }
        }

        public void CopyTo(string dest, bool overwriteExisting = false)
        {
            if (!Validation.CanWrite(dest, false))
            {
                DebugConsole.ThrowError($"Cannot copy \"{Name}\" to \"{dest}\": modifying the contents of the destination folder is not allowed.");
                return;
            }
            innerInfo.CopyTo(dest, overwriteExisting);
        }

        public void Delete()
        {
            if (!Validation.CanWrite(innerInfo.FullName, false))
            {
                DebugConsole.ThrowError($"Cannot delete file \"{Name}\": modifying the files in this folder/with this extension is not allowed.");
                return;
            }
            innerInfo.Delete();
        }
    }
}
