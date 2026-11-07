using UnityEngine;

namespace FlashBeat.Persistence
{
    /// <summary>
    /// Where the score store is persisted (issue #189).
    ///
    /// The game keeps the best scores in `PlayerPrefs` (one key for the new
    /// format, one for the legacy format, one for the migration flag). The
    /// tests put an in-memory storage into <see cref="ScorePersistence.Storage"/>,
    /// so a test run never reads or writes the save data of the player.
    /// </summary>
    public interface IScoreStorage
    {
        /// <summary>Read a string, or the fallback when it is not there.</summary>
        string GetString(string key, string fallback);

        /// <summary>Write a string.</summary>
        void SetString(string key, string value);

        /// <summary>Read an int, or the fallback when it is not there.</summary>
        int GetInt(string key, int fallback);

        /// <summary>Write an int.</summary>
        void SetInt(string key, int value);

        /// <summary>Forget a key (string and int).</summary>
        void DeleteKey(string key);

        /// <summary>Flush the pending writes.</summary>
        void Save();
    }

    /// <summary>The storage of the game: `PlayerPrefs`.</summary>
    public sealed class PlayerPrefsScoreStorage : IScoreStorage
    {
        public string GetString(string key, string fallback)
        {
            return PlayerPrefs.GetString(key, fallback);
        }

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
        }

        public int GetInt(string key, int fallback)
        {
            return PlayerPrefs.GetInt(key, fallback);
        }

        public void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
        }

        public void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
