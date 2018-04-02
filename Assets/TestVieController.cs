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

        mRecycleTable = new UIRecycleTable<ItemController>(mScrollView, OnLoadItem, OnUpdateItem, OnDeleteItem, false);
        mRecycleTable.itemCount = mDatas.Count;
        mRecycleTable.itemIntervalPixel = 20;
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

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (mRecycleTable != null && !mRecycleTable.isNoneScrollView)
        {
            if (!Application.isPlaying) mRecycleTable.mCalculatedBounds = false;
            mRecycleTable.mCalculatedBounds = true;
            Bounds b = mRecycleTable.itemsBounds;
            Gizmos.matrix = mRecycleTable.itemTrans.localToWorldMatrix;
            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(new Vector3(b.center.x, b.center.y, b.min.z), new Vector3(b.size.x, b.size.y, 0f));
        }
    }
#endif
}
