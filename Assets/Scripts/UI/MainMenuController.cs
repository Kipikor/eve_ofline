using EveOffline.Game;
using EveOffline.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EveOffline.UI
{
    public class MainMenuController : MonoBehaviour
    {
        public void OnSlotPlay(int slotIndex)
        {
            if (!SaveSystem.HasSave(slotIndex))
            {
                SaveSystem.CreateOrOverwrite(slotIndex);
            }

            SceneManager.LoadScene(SceneNames.Station);
        }

        public void OnSlotDelete(int slotIndex)
        {
            SaveSystem.DeleteSave(slotIndex);
        }

        public string GetSlotLabel(int slotIndex)
        {
            return SaveSystem.HasSave(slotIndex) ? SaveSystem.GetMeta(slotIndex) : "Пустой слот";
        }
    }
}


