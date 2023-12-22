using UnityEngine;

namespace Tachyoid.Objects {
    public interface ButtonTriggerReceiver {
        void TriggerEnter(ButtonController button, bool ftl, bool isReversingTime = false);
        void TriggerStay(ButtonController button, bool ftl, bool isReversingTime = false);
        void TriggerExit(ButtonController button, bool ftl, bool isReversingTime = false);
    }
}
