using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemController : IRecycleTable
{
    public ItemController(GameObject pGo)
    {
        mGameObject = pGo;
        mSprite = mGameObject.GetComponentInChildren<UISprite>();
        mLabel = mGameObject.GetComponentInChildren<UILabel>();
    }

    GameObject mGameObject;
    int mIndex;
    UISprite mSprite;
    UILabel mLabel;

    public Transform itemTransform
    {
        get
        {
            return mGameObject.transform;
        }
    }

    public int itemIndex { set; get; }
    public Bounds bounds { set; get; }

    public void SetData(string pName)
    {
        mSprite.height = Random.Range(50, 150);
        mLabel.text = pName;
    }
}
