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
    UISprite mSprite;
    UILabel mLabel;

    public Transform itemTransform
    {
        get
        {
            return mGameObject.transform;
        }
    }

    public RecycleTableInfo recycleTablInfo { set; get; }

    public void SetData(TestInfo pInfo)
    {
        info = pInfo;
        mLabel.text = pInfo.name.ToString();
        mSprite.SetDimensions((int)pInfo.spriteSize.x, (int)pInfo.spriteSize.y);
    }
    public TestInfo info { set; get; }
}
