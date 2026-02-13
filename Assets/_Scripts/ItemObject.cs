using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ItemObject : MonoBehaviour
{
    public Equipment itemData;

    public void Setup(Equipment data)
    {
        itemData = data;
        if (TryGetComponent(out SpriteRenderer sr))
        {
            sr.sprite = data.itemSprite;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (itemData == null) return;

        Color rarityColor = itemData.rarity switch
        {
            ItemRarity.Normal => Color.white,
            ItemRarity.Rare => Color.cyan,
            ItemRarity.Epic => new Color(0.6f, 0.2f, 1f),
            ItemRarity.Unique => Color.yellow,
            ItemRarity.Legendary => Color.red,
            _ => Color.white
        };

        Gizmos.color = rarityColor;
        Gizmos.DrawWireCube(transform.position, transform.localScale * 0.7f);

        GUIStyle style = new GUIStyle();
        style.normal.textColor = rarityColor;
        style.fontSize = 12;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        
        string labelText = $"{itemData.itemName}\n{itemData.GetStatString()}";
        Handles.Label(transform.position + Vector3.up * 0.6f, labelText, style);
    }
#endif
}