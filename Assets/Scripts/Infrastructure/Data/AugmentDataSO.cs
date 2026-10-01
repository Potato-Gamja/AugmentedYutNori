using UnityEngine;

[CreateAssetMenu(menuName = "Augment/Augment Data")]
public class AugmentDataSO : ScriptableObject
{
    [SerializeField] private string augmentId;
    [SerializeField] private string augmentName;
    [TextArea] [SerializeField] private string augmentDescription;

    public string AugmentId => augmentId;
    public string AugmentName => augmentName;
    public string AugmentDescription => augmentDescription;
}