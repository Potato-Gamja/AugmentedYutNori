using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Augment/Augment Catalog")]
public class AugmentCatalogSO : ScriptableObject
{
    [SerializeField] private List<AugmentDataSO> augments;
    public AugmentDataSO Find(string id) => augments.Find(a => a.AugmentId == id);
}