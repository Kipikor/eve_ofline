using UnityEngine;

namespace EveOffline.UI
{
    public class MainMenuSlotButton : MonoBehaviour
    {
        private MainMenuController mainMenuController;
        private int slotIndex;
        private bool delete;

        public void Configure(MainMenuController controller, int index, bool isDelete)
        {
            mainMenuController = controller;
            slotIndex = index;
            delete = isDelete;
        }

        public void Invoke()
        {
            if (mainMenuController == null)
            {
                mainMenuController = Object.FindFirstObjectByType<MainMenuController>();
            }
            if (mainMenuController == null) return;

            if (delete)
            {
                mainMenuController.OnSlotDelete(slotIndex);
            }
            else
            {
                mainMenuController.OnSlotPlay(slotIndex);
            }
        }
    }
}


