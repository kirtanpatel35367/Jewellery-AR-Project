// JewelleryDatabase.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "JewelleryDatabase", menuName = "AR/Jewellery Database")]
public class JewelleryDatabase : ScriptableObject
{
    public List<JewelleryItem> items = new List<JewelleryItem>();
}