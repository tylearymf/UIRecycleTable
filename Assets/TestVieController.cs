using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine;

public class TestVieController : MonoBehaviour
{
    public UIScrollView mScrollView;
    UIRecycleTable<ItemController> mRecycleTable;
    public GameObject mPrefab;
    List<TestInfo> mDatas;
    ItemController mItemController10;

    void Start()
    {
        mDatas = Enumerable.Range(0, 2).Select(x =>
        {
            var t = new TestInfo()
            {
                name = x.ToString(),
                spriteSize = new Vector2(UnityEngine.Random.Range(50, 150), UnityEngine.Random.Range(50, 150)),
            };
            return t;
        }).ToList();

        mRecycleTable = new UIRecycleTable<ItemController>(mScrollView, OnLoadItem, OnUpdateItem, OnDeleteItem);
        mRecycleTable.itemIntervalPixel = 20;
        mRecycleTable.onStartTarget = pOffset =>
        {
            Debug.LogError("OnStartTarget : " + pOffset);
        };
        mRecycleTable.onEndTarget = pOffset =>
        {
            Debug.LogError("OnEndTarget : " + pOffset);
        };
        mRecycleTable.ResetPosition(mDatas.Count);
    }

    private void OnDeleteItem(ItemController pItem)
    {
        NGUITools.Destroy(pItem.itemTransform);
    }

    private void OnUpdateItem(ItemController pItem, int pIndex)
    {
        if (pIndex == 10) mItemController10 = pItem;

        pItem.SetData(mDatas[pIndex]);
    }

    private ItemController OnLoadItem(int pIndex)
    {
        var tPrefab = Instantiate(mPrefab);
        var tItemCtroller = new ItemController(tPrefab);
        return tItemCtroller;
    }

    [ContextMenu("ChangeSize")]
    public void ChangeSize()
    {
        if (mItemController10 == null) return;
        var tInfo = mItemController10.info;
        tInfo.spriteSize = new Vector2(UnityEngine.Random.Range(50, 150), UnityEngine.Random.Range(50, 150));
        mItemController10.SetData(tInfo);
    }

    [ContextMenu("RefreshItem")]
    public void RefreshItem()
    {
        if (mRecycleTable == null) return;
        mRecycleTable.ForceRefreshItem();
    }

    public int index;
    [ContextMenu("MoveIndex")]
    public void MoveIndex()
    {
        mRecycleTable.MoveToItemByIndex(index);
    }
}

public class TestInfo
{
    public string name { set; get; }
    public Vector2 spriteSize { set; get; }
}