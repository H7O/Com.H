using Com.H.IO;
using Com.H.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Com.H.Net.Mail
{
    public class MailAttachment : IDisposable
    {
        private bool disposedValue;

        public string FilePath { get; set; }
        public string FileName { get; set; }
        public FileStream Stream { get; set; }
        private string TempPath { get; set; }

        public MailAttachment(Stream stream, string fileName,
            CancellationToken? cancellationToken = null)
            => Init(stream, fileName, cancellationToken);
        
        

        private void Init(Stream stream, string fileName, 
            CancellationToken? cancellationToken = null)
        {
            this.TempPath = Path.Combine(Path.GetTempPath(),
                Guid.NewGuid().ToString() + ".att");
            if (File.Exists(this.TempPath)) 
                File.Delete(this.TempPath);
            using (var f = File.OpenWrite(this.TempPath
                .EnsureParentDirectory()))
            {
                if (cancellationToken != null)
                    stream.CopyToAsync(f,
                        (CancellationToken)cancellationToken)
                        .GetAwaiter().GetResult();
                else stream.CopyTo(f);
                f.Close();
            }

            this.Stream = new FileStream(this.TempPath, 
                FileMode.Open, FileAccess.Read, 
                FileShare.None, 4096, 
                FileOptions.DeleteOnClose);
            this.FileName = fileName;
        }

        public MailAttachment(string filePath)
        {
            if (!File.Exists(filePath)) 
                throw new FileNotFoundException(filePath);
            using var f = File.OpenRead(filePath);
            Init(f, Path.GetFileName(filePath));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        if (File.Exists(this.TempPath))
                        {
                            this.Stream.Close();
                            this.Stream.Dispose();
                        }
                    }
                    catch { }
                    try
                    {
                        if (File.Exists(this.TempPath))
                            File.Delete(this.TempPath);
                    }
                    catch { }

                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~MailAttachment()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
    public class MailAttachmentCollection : IDisposable
    {
        private bool disposedValue;

        public List<MailAttachment> List { get; init; }

        public MailAttachmentCollection()
            => this.List = new List<MailAttachment>();
        public void Add(string filePath) 
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException(filePath);
            this.List.Add(new MailAttachment(filePath));
        }

        public void Add(Stream stream, string fileName)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            this.List.Add(new MailAttachment(stream, fileName));
        }

        public void RemoveAttachment(string fileNameOrFilePath)
        {
            if (string.IsNullOrWhiteSpace(fileNameOrFilePath))
                return;
            var toBeRemoved = this.List.FirstOrDefault(
                x => x.FileName.EqualsIgnoreCase(fileNameOrFilePath)
                || x.FilePath.EqualsIgnoreCase(fileNameOrFilePath));
            if (toBeRemoved == null) return;
            toBeRemoved.Dispose();
            this.List.Remove(toBeRemoved);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    if (this.List != null)
                        foreach (var item in this.List)
                            item.Dispose();
                    this.List.Clear();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~MailAttachmentCollection()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
