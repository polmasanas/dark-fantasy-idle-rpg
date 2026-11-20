using System.Text;
using DarkFantasy.Core.Random;
using DarkFantasy.Core.Save;
using DarkFantasy.Core.Session;
using DarkFantasy.Core.Stats;
using NUnit.Framework;

namespace DarkFantasy.Core.Tests.Save
{
    public class SaveStoreTests
    {
        private static SaveData Data(double gold)
        {
            var state = GameState.NewRun(ClassId.Fighter);
            new GameSession(TestConfig.Load(), state, new Pcg32(1), new DarkFantasy.Core.Events.EventBus());
            state.Gold = gold;
            return new SaveData { State = state, RngState = 1, RngIncrement = 3 };
        }

        [Test]
        public void FirstLaunch_NoFiles_NoData()
        {
            var r = new SaveStore(new MemoryFileStore()).Load();
            Assert.IsNull(r.Data);
            Assert.IsEmpty(r.Rejected);
        }

        [Test]
        public void SaveThenLoad_ReturnsLatest()
        {
            var store = new SaveStore(new MemoryFileStore());
            store.Save(Data(10));
            store.Save(Data(20));
            var r = store.Load();
            Assert.AreEqual(20, r.Data.State.Gold);
            Assert.AreEqual(SaveStore.MainFile, r.LoadedFrom);
        }

        [Test]
        public void Rotation_KeepsThreeBackups()
        {
            var fs = new MemoryFileStore();
            var store = new SaveStore(fs);
            for (int i = 1; i <= 6; i++) store.Save(Data(i));
            Assert.IsTrue(fs.Exists("save.bak1") && fs.Exists("save.bak2") && fs.Exists("save.bak3"));
            Assert.IsFalse(fs.Exists("save.bak4"));
            Assert.IsFalse(fs.Exists(SaveStore.TempFile));
            var ser = new SaveSerializer();
            Assert.AreEqual(5, ser.Deserialize(fs.Read("save.bak1")).State.Gold);
            Assert.AreEqual(3, ser.Deserialize(fs.Read("save.bak3")).State.Gold);
        }

        [Test]
        public void CorruptMain_FallsBackToBackup()
        {
            var fs = new MemoryFileStore();
            var store = new SaveStore(fs);
            store.Save(Data(1));
            store.Save(Data(2));
            fs.Write(SaveStore.MainFile, Encoding.UTF8.GetBytes("DFRS\u0001broken"));
            var r = store.Load();
            Assert.AreEqual(1, r.Data.State.Gold);
            Assert.AreEqual("save.bak1", r.LoadedFrom);
            CollectionAssert.Contains(r.Rejected, SaveStore.MainFile);
        }

        [Test]
        public void CrashBetweenRotateAndRename_LoadsBackup()
        {
            var fs = new MemoryFileStore();
            var store = new SaveStore(fs);
            store.Save(Data(7));
            // simulate a crash after the main file was rotated away and before the new one was renamed in
            fs.Replace(SaveStore.MainFile, "save.bak1");
            fs.Write(SaveStore.TempFile, Encoding.UTF8.GetBytes("half-written"));
            var r = store.Load();
            Assert.AreEqual(7, r.Data.State.Gold);
        }

        [Test]
        public void EverythingCorrupt_NoData_AllRejected()
        {
            var fs = new MemoryFileStore();
            fs.Write(SaveStore.MainFile, new byte[] { 1, 2, 3 });
            fs.Write("save.bak1", new byte[] { 4, 5, 6 });
            var r = new SaveStore(fs).Load();
            Assert.IsNull(r.Data);
            Assert.AreEqual(2, r.Rejected.Count);
        }

        [Test]
        public void DirectoryStore_RoundTripOnDisk()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dfrs-test-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var store = new SaveStore(new DirectoryFileStore(dir));
                store.Save(Data(1));
                store.Save(Data(2));
                Assert.AreEqual(2, store.Load().Data.State.Gold);
                Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(dir, "save.bak1")));
            }
            finally
            {
                System.IO.Directory.Delete(dir, true);
            }
        }

        [Test]
        public void DeleteAll_RemovesEveryFile()
        {
            var fs = new MemoryFileStore();
            var store = new SaveStore(fs);
            store.Save(Data(1));
            store.Save(Data(2));
            store.DeleteAll();
            Assert.IsNull(store.Load().Data);
        }
    }
}
