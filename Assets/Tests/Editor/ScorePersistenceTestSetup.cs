using System.Collections.Generic;
using FlashBeat.Persistence;
using NUnit.Framework;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Keeps the whole EditMode run away from the real save data (issue #189).
    ///
    /// `ScorePersistence` persists through `PlayerPrefs` in the game. A test
    /// that saved through it would write into the save data of the developer,
    /// and the scores of one test run would leak into the next one (a test
    /// that asserts "this song has no record yet" would fail on the second
    /// run). So the whole test domain gets an in-memory storage instead.
    /// Fixtures that need a pristine store swap in a fresh
    /// <see cref="MemoryScoreStorage"/> of their own.
    /// </summary>
    [SetUpFixture]
    public class ScorePersistenceTestSetup
    {
        /// <summary>Swap the real storage for an in-memory one, for every fixture.</summary>
        [OneTimeSetUp]
        public void UseMemoryStorageForTheWholeRun()
        {
            ScorePersistence.Storage = new MemoryScoreStorage();
        }

        /// <summary>Give the real storage back when the run is over.</summary>
        [OneTimeTearDown]
        public void RestoreTheRealStorage()
        {
            ScorePersistence.Unload();
            ScorePersistence.Storage = new PlayerPrefsScoreStorage();
        }
    }

    /// <summary>An in-memory `IScoreStorage` for the tests.</summary>
    public class MemoryScoreStorage : IScoreStorage
    {
        readonly Dictionary<string, string> strings = new Dictionary<string, string>();
        readonly Dictionary<string, int> ints = new Dictionary<string, int>();

        public string GetString(string key, string fallback)
        {
            string value;
            return strings.TryGetValue(key, out value) ? value : fallback;
        }

        public void SetString(string key, string value)
        {
            strings[key] = value;
        }

        public int GetInt(string key, int fallback)
        {
            int value;
            return ints.TryGetValue(key, out value) ? value : fallback;
        }

        public void SetInt(string key, int value)
        {
            ints[key] = value;
        }

        public void DeleteKey(string key)
        {
            strings.Remove(key);
            ints.Remove(key);
        }

        public void Save()
        {
            // Nothing to flush: the memory is the file.
        }
    }
}
