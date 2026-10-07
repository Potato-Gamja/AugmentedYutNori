using UnityEngine;

public class StateHashSerializeTest : MonoBehaviour
{
    private void Start()
    {
        var report = new StateHashReport { seq = 3, stateHash = "0123456789abcdef" };
        string json = MessageSerializer.Serialize(MessageType.STATE_HASH, report);
        Debug.Log(json);
    }
}