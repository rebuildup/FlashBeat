using UnityEngine;

namespace FlashBeat.Persistence
{
    /// <summary>
    /// Where the display settings of a play session are persisted (issue-162 D-1).
    ///
    /// The game keeps the Key Guide mode and the Lyrics switch in `PlayerPrefs`
    /// (two keys). The tests put an in-memory storage into
    /// <see cref="FlashBeat.UI.V5.PlaySession.Storage"/>, so a test run never
    /// reads or writes the save data of the player.
    /// </summary>
    public interface IDisplaySettingsStorage
    {
        /// <summary>Read an int, or the fallback when it is not there.</summary>
        int GetInt(string key, int fallback);

        /// <summary>Write an int.</summary>
        void SetInt(string key, int value);

        /// <summary>Whether the key is there at all.</summary>
        bool HasKey(string key);

        /// <summary>Flush the pending writes.</summary>
        void Save();
    }

    /// <summary>The storage of the game: `PlayerPrefs`.</summary>
    public sealed class PlayerPrefsDisplaySettingsStorage : IDisplaySettingsStorage
    {
        public int GetInt(string key, int fallback)
        {
            return PlayerPrefs.GetInt(key, fallback);
        }

        public void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
        }

        public bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
