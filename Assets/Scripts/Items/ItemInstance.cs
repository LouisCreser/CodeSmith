using System;

[Serializable]
public class ItemInstance
{
    public string itemId;
    public int hammerProgress;

    public ItemInstance(string itemId)
    {
        this.itemId = itemId;
        hammerProgress = 0;
    }

    public ItemInstance(string itemId, int hammerProgress)
    {
        this.itemId = itemId;
        this.hammerProgress = hammerProgress;
    }

    public ItemInstance Clone()
    {
        return new ItemInstance(itemId, hammerProgress);
    }

    public void ResetProgress()
    {
        hammerProgress = 0;
    }
}