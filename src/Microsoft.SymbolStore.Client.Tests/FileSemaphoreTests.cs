using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.SymbolStore.Client
{
    public partial class Tests
    {
        const string FileName = "test";
        const string LockFile = "test.sem";

        [Fact]
        public void TestSemaphoreLocking()
        {
            // Clean up last run
            if (File.Exists(LockFile))
                File.Delete(LockFile);

            Assert.False(File.Exists(LockFile));

            using (IDisposable lck = FileSemaphore.LockFile(FileName))
            {

                Assert.True(File.Exists(LockFile));

                for (int i = 0; i < 10; i++)
                    Assert.Null(FileSemaphore.TryLockFile(FileName));

                Task t = ReleaseLock(lck);
                t.Wait();
                Assert.False(File.Exists(LockFile));
                

            } // lck will be double-disposed, make sure that doesn't throw or cause problems

            Assert.False(File.Exists(LockFile));
            FileSemaphore.TryLockFile(FileName).Dispose();  // Throws if we couldn't take the lock
            Assert.False(File.Exists(LockFile));
        }

        [Fact]
        public void TestBackgroundLocking()
        {
            // Clean up last run
            if (File.Exists(LockFile))
                File.Delete(LockFile);

            Assert.False(File.Exists(LockFile));

            EventWaitHandle handle = new EventWaitHandle(false, EventResetMode.ManualReset);
            Task backgroundLockFile = new Task(() => { handle.Set(); FileSemaphore.LockFile(FileName).Dispose(); });
            using (IDisposable lck = FileSemaphore.LockFile(FileName))
            {
                backgroundLockFile.Start();
                handle.WaitOne(); // 100% sure the task has had time to start

                Assert.False(backgroundLockFile.Wait(100));
                Assert.False(backgroundLockFile.IsCompleted);
            }

            Assert.True(backgroundLockFile.Wait(250));
            Assert.False(File.Exists(LockFile));
        }
        
        private async Task ReleaseLock(IDisposable lck)
        {
            await Task.Delay(100);
            lck.Dispose();
        }
        
    }
}
