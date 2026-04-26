using UnityEngine;
using UnityEngine.AddressableAssets;

[System.Serializable]
public class JewelryItem
{
    public string itemName;
    public Sprite thumbnailImage;
    public AssetReferenceGameObject jewelryReference;
    public float defaultScale = 1f;
}
