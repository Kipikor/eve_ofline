using System;
using UnityEngine;

namespace EveOffline.Save
{
    public static class SaveSystem
    {
        public const int MaxSlots = 4;

        private static string SlotKey(int slotIndex) => $"SaveSlot_{slotIndex}";
        private static string SlotMetaKey(int slotIndex) => $"SaveSlot_{slotIndex}_Meta";

        public static bool HasSave(int slotIndex)
        {
            ValidateSlot(slotIndex);
            return PlayerPrefs.HasKey(SlotKey(slotIndex));
        }

        public static void DeleteSave(int slotIndex)
        {
            ValidateSlot(slotIndex);
            PlayerPrefs.DeleteKey(SlotKey(slotIndex));
            PlayerPrefs.DeleteKey(SlotMetaKey(slotIndex));
            PlayerPrefs.Save();
        }

        public static void CreateOrOverwrite(int slotIndex)
        {
            ValidateSlot(slotIndex);
            // Пока сохраняем только метаданные — время создания и версию
            PlayerPrefs.SetString(SlotKey(slotIndex), DateTime.UtcNow.ToString("o"));
            PlayerPrefs.SetString(SlotMetaKey(slotIndex), "EVE Offline v0.1");
            PlayerPrefs.Save();
        }

        public static string GetMeta(int slotIndex)
        {
            ValidateSlot(slotIndex);
            return PlayerPrefs.GetString(SlotMetaKey(slotIndex), "Пусто");
        }

        private static void ValidateSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex), $"Слот должен быть в диапазоне [0,{MaxSlots - 1}]");
            }
        }
    }
}

