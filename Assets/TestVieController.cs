using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class TestVieController : MonoBehaviour
{
    public UIScrollView mScrollView;
    UIRecycleTable<ItemController> mRecycleTable;
    public GameObject mPrefab;
    List<string> mDatas;

    void Start()
    {
        mDatas = Enumerable.Range(0, 100).Select(x => x.ToString()).ToList();

        mRecycleTable = new UIRecycleTable<ItemController>(mScrollView, OnLoadItem, OnUpdateItem, OnDeleteItem);
        mRecycleTable.itemCount = mDatas.Count;
        mRecycleTable.MoveToItemByIndex(0);
    }

    private void OnDeleteItem(ItemController pItem)
    {
        NGUITools.Destroy(pItem.itemTransform);
    }

    private void OnUpdateItem(ItemController pItem, int pIndex)
    {
        pItem.SetData(mDatas[pIndex]);
    }

    private ItemController OnLoadItem()
    {
        var tPrefab = Instantiate(mPrefab);
        var tItemCtroller = new ItemController(tPrefab);
        return tItemCtroller;
    }
}
