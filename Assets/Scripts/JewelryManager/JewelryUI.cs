using UnityEngine;
using UnityEngine.UI;

public class JewelryUI : MonoBehaviour
{
    [Header("Manager")]
    public JewelryManager jewelryManager;

    [Header("Earring Buttons")]
    public Button goldButton;
    public Button diamondButton;
    public Button removeButton;

    void Start()
    {
        // Connect buttons to manager
        goldButton.onClick.AddListener(() => jewelryManager.EquipEarrings("Gold"));
        diamondButton.onClick.AddListener(() => jewelryManager.EquipEarrings("Diamond"));
        removeButton.onClick.AddListener(() => jewelryManager.RemoveEarrings());
    }
}