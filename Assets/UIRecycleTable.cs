using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public interface IRecycleTable
{
    int itemIndex { set; get; }
    Transform itemTransform { get; }
    Bounds bounds { set; get; }
}

public class UIRecycleTable<T> : IDisposable where T : IRecycleTable
{
    public enum Direction
    {
        None,
        Left,
        Bottom,
        Right,
        Top,
    }

    #region 构造方法
    protected UIRecycleTable() { }

    /// <summary>
    /// UIRecycleTable唯一入口
    /// </summary>
    /// <param name="pScrollView"></param>
    public UIRecycleTable(UIScrollView pScrollView, OnLoadItem pOnLoadItem, OnUpdateItem pOnUpdateItem, OnDeleteItem pOnDeleteItem)
    {
        if (pScrollView == null) return;

        scrollView = pScrollView;
        panel = scrollView.panel;
        scrollViewTrans = scrollView.transform;
        onLoadItem = pOnLoadItem;
        onUpdateItem = pOnUpdateItem;
        onDeleteItem = pOnDeleteItem;

        Init();
    }
    #endregion

    #region 事件
    public delegate T OnLoadItem();
    public delegate void OnUpdateItem(T pItem, int pIndex);
    public delegate void OnDeleteItem(T pItem);

    /// <summary>
    /// 加载Item
    /// </summary>
    public OnLoadItem onLoadItem;
    /// <summary>
    /// 刷新Item
    /// </summary>
    public OnUpdateItem onUpdateItem;
    /// <summary>
    /// 删除Item
    /// </summary>
    public OnDeleteItem onDeleteItem;
    #endregion

    #region 字段
    protected int mItemMaxCount;
    protected Bounds mItemsBounds;
    protected bool mCalculatedBounds;
    #endregion

    #region 属性
    public UIScrollView scrollView
    {
        protected set;
        get;
    }
    public UIPanel panel
    {
        protected set;
        get;
    }
    public Transform scrollViewTrans
    {
        protected set;
        get;
    }
    public Transform itemTrans
    {
        protected set;
        get;
    }
    public UIWidget dragRegion
    {
        protected set;
        get;
    }
    protected Transform cacheTrans
    {
        set;
        get;
    }
    protected Dictionary<Transform, T> itemControllerDic
    {
        set;
        get;
    }
    protected List<T> itemControllers
    {
        get
        {
            return itemControllerDic.Values.ToList();
        }
    }
    /// <summary>
    /// 是否有ScrollView
    /// </summary>
    public bool isNoneScrollView
    {
        get { return !scrollView; }
    }
    /// <summary>
    /// 是否有BoxCollider
    /// </summary>
    public bool isNoneBoxCollider
    {
        get { return !dragRegion; }
    }
    /// <summary>
    /// 是否有Item
    /// </summary>
    public bool isNoneChild
    {
        get { return !itemTrans || itemTrans.childCount == 0; }
    }
    public int childCount
    {
        get { return isNoneChild ? 0 : itemTrans.childCount; }
    }
    /// <summary>
    /// Item数量
    /// </summary>
    public int itemCount
    {
        set
        {
            mItemMaxCount = Mathf.Max(0, value);
        }
        get
        {
            return Mathf.Max(0, mItemMaxCount);
        }
    }
    /// <summary>
    /// Item之间的间距
    /// </summary>
    public int itemIntervalPixel
    {
        set;
        get;
    }
    protected Bounds itemsBounds
    {
        get
        {
            if (mCalculatedBounds)
            {
                mCalculatedBounds = false;
                mItemsBounds = NGUIMath.CalculateRelativeWidgetBounds(itemTrans, false);
            }
            return mItemsBounds;
        }
    }
    protected Bounds panelBounds
    {
        set;
        get;
    }
    #endregion

    #region 初始化
    /// <summary>
    /// 初始化
    /// </summary>
    void Init()
    {
        if (isNoneScrollView) return;
        NGUITools.DestroyChildren(scrollViewTrans);

        scrollView.restrictWithinPanel = true;
        scrollView.disableDragIfFits = false;

        if (!itemTrans)
        {
            itemTrans = NGUITools.AddChild(scrollView.gameObject).transform;
            itemTrans.name = "Items";
        }

        if (!dragRegion)
        {
            dragRegion = NGUITools.AddChild<UIWidget>(scrollView.gameObject);
            dragRegion.name = "DragRegion";
            dragRegion.gameObject.AddMissingComponent<BoxCollider>();
            dragRegion.autoResizeBoxCollider = true;
            dragRegion.gameObject.AddComponent<UIDragScrollView>();
            UpdateBoxColliderWidget();
        }

        if (!cacheTrans)
        {
            cacheTrans = NGUITools.AddChild(scrollViewTrans.parent.gameObject).transform;
            cacheTrans.name = "UIRecycleTableCache";
            cacheTrans.gameObject.SetActive(false);
        }

        var tClipRegion = panel.finalClipRegion;
        panelBounds = new Bounds(new Vector3(tClipRegion.x, tClipRegion.y, 0), new Vector3(tClipRegion.z, tClipRegion.w, 0));

        RegisterEvent();
        MoveToItemByIndex(0);
    }

    /// <summary>
    /// 注册事件
    /// </summary>
    void RegisterEvent()
    {
        if (isNoneScrollView) return;
        RemoveEvent();

        panel.onClipMove += OnClipMove;
        scrollView.onDragStarted += OnDragStarted;
        scrollView.onDragFinished += OnDragFinished;
        scrollView.onMomentumMove += OnMomentumMove;
        scrollView.onStoppedMoving += OnStoppedMoving;
    }

    /// <summary>
    /// 移除事件
    /// </summary>
    void RemoveEvent()
    {
        if (isNoneScrollView) return;

        panel.onClipMove -= OnClipMove;
        scrollView.onDragStarted -= OnDragStarted;
        scrollView.onDragFinished -= OnDragFinished;
        scrollView.onMomentumMove -= OnMomentumMove;
        scrollView.onStoppedMoving -= OnStoppedMoving;
    }
    #endregion

    #region 主要逻辑
    /// <summary>
    /// 变更BoxCollider
    /// </summary>
    void UpdateBoxColliderWidget(T pItemController = default(T), Direction pDirection = Direction.None)
    {
        if (isNoneScrollView || isNoneBoxCollider) return;

        if (isNoneChild || (itemCount == 0))
        {
            //获取ScrollView的宽高设置BoxCollider
            dragRegion.SetAnchor(scrollView.gameObject, 0, 0, 0, 0, 1, 0, 1, 0);
            dragRegion.SetAnchor(null, 0, 0, 0, 0);
        }
        else if (pItemController != null)
        {
            //根据最后一个或者第一个item的位置，只修改BoxCollider的Bottom或Top

            var tPos = dragRegion.cachedTransform.localPosition;
            var tBounds = pItemController.bounds;
            tBounds.center += pItemController.itemTransform.localPosition;
            switch (pDirection)
            {
                case Direction.Bottom:
                    dragRegion.cachedTransform.AddLocalPosY(-tBounds.size.y / 2);
                    dragRegion.SetDimensions(Mathf.FloorToInt(dragRegion.localSize.x), Mathf.FloorToInt(dragRegion.localSize.y + tBounds.size.y + itemIntervalPixel));
                    break;
                case Direction.Top:
                    break;
            }
        }
    }

    /// <summary>
    /// Panel裁剪时触发
    /// </summary>
    /// <param name="pPanel"></param>
    private void OnClipMove(UIPanel pPanel)
    {
        if (isNoneScrollView || itemCount == 0) return;

        mCalculatedBounds = true;
        var tPanelOffset = panel.CalculateConstrainOffset(itemsBounds.min, itemsBounds.max);
        if (tPanelOffset.y > 1)
        {
            Debug.Log("下拉出框");
            AddItem(Direction.Top);
        }
        else if (tPanelOffset.y < -1)
        {
            Debug.Log("上拖出框");
            AddItem(Direction.Bottom);
        }
        else
        {
            Debug.Log("处于框中");
        }
    }

    /// <summary>
    /// ScrollView停止移动时触发
    /// </summary>
    private void OnStoppedMoving()
    {
        if (isNoneScrollView) return;
    }

    /// <summary>
    /// ScrollView移动时触发（每帧都会触发）
    /// </summary>
    private void OnMomentumMove()
    {
        if (isNoneScrollView) return;
    }

    /// <summary>
    /// 手指松开时触发
    /// </summary>
    private void OnDragFinished()
    {
        if (isNoneScrollView) return;

        Debug.LogError(string.Format("panelBounds.center:{0}\nsize:{1}", panelBounds.center, panelBounds.size));
        for (int i = 0; i < childCount; i++)
        {
            var tKey = itemTrans.GetChild(i);
            T tCtrl = default(T);
            if (!itemControllerDic.TryGetValue(tKey, out tCtrl)) continue;
            var tItemBounds = tCtrl.bounds;
            tItemBounds.center += tCtrl.itemTransform.localPosition + scrollViewTrans.localPosition;
            if (panelBounds.Intersects(tItemBounds)) continue;
            Debug.LogError(string.Format("tItemBounds.name:{2}\ncenter:{0}\nsize:{1}", tItemBounds.center, tItemBounds.size, tCtrl.itemIndex));
            MoveItemToCache(tCtrl);
        }
    }

    /// <summary>
    /// 手指按下时触发
    /// </summary>
    private void OnDragStarted()
    {
        if (isNoneScrollView) return;
    }
    #endregion

    void AddItem(Direction pDirection)
    {
        var tItemControllers = itemControllers;
        if (tItemControllers == null || tItemControllers.Count == 0) return;
        tItemControllers.Sort((x, y) => x.itemIndex.CompareTo(y.itemIndex));
        T tTempItemCtrl = default(T);
        var tItemIndex = 0;
        switch (pDirection)
        {
            case Direction.Bottom:
                tTempItemCtrl = tItemControllers[tItemControllers.Count - 1];
                tItemIndex = tTempItemCtrl.itemIndex + 1;
                if (tItemIndex >= itemCount) return;
                break;
            case Direction.Top:
                tTempItemCtrl = tItemControllers[0];
                tItemIndex = tTempItemCtrl.itemIndex - 1;
                if (tItemIndex < 0) return;
                break;
        }
        if (tTempItemCtrl == null) return;
        var tItemCtrl = GetItemControllerByCache(tItemIndex);
        onUpdateItem(tItemCtrl, tItemIndex);

        var tTempItemBounds = NGUIMath.CalculateRelativeWidgetBounds(tTempItemCtrl.itemTransform, false);
        tTempItemBounds.center += tTempItemCtrl.itemTransform.localPosition;

        var tItemBounds = NGUIMath.CalculateRelativeWidgetBounds(tItemCtrl.itemTransform, false);
        tItemCtrl.bounds = tItemBounds;

        var tItemOffsetY = 0F;
        switch (pDirection)
        {
            case Direction.Bottom:
                tItemOffsetY = tTempItemBounds.min.y - tItemBounds.max.y - itemIntervalPixel;
                break;
            case Direction.Top:
                tItemOffsetY = tTempItemBounds.max.y - tItemBounds.min.y + itemIntervalPixel;
                break;
        }
        tItemCtrl.itemTransform.AddLocalPosY(tItemOffsetY);

        UpdateBoxColliderWidget(tItemCtrl, pDirection);
        mCalculatedBounds = true;
        Debug.LogError("add item index :" + tItemIndex);
    }

    #region 定位
    public void MoveToItemByIndex(int pIndex)
    {
        if (isNoneScrollView) return;

        MoveAllItemToCache();

        if (itemCount == 0) return;
        var tPanelBounds = panelBounds;
        var tPreviousMaxY = tPanelBounds.max.y;
        while (tPreviousMaxY > tPanelBounds.min.y)
        {
            var tItemCtrl = GetItemControllerByCache(pIndex);
            if (onUpdateItem != null) onUpdateItem(tItemCtrl, pIndex++);
            var tItemBounds = NGUIMath.CalculateRelativeWidgetBounds(tItemCtrl.itemTransform, false);
            tItemCtrl.bounds = tItemBounds;

            var tItemOffsetY = tPreviousMaxY - tItemBounds.max.y;
            tItemCtrl.itemTransform.AddLocalPosY(tItemOffsetY);
            tPreviousMaxY = tPreviousMaxY - tItemBounds.size.y - itemIntervalPixel;
        }

        mCalculatedBounds = true;
        var tItemsBounds = itemsBounds;
        var tWidth = tItemsBounds.max.x - tItemsBounds.min.x;
        var tHeight = tItemsBounds.max.y - tItemsBounds.min.y;
        dragRegion.SetDimensions(Mathf.FloorToInt(tWidth), Mathf.FloorToInt(tHeight));
        dragRegion.cachedTransform.SetLocalPos(tItemsBounds.center);
    }
    #endregion

    T GetItemControllerByCache(int pIndex)
    {
        if (itemControllerDic == null)
        {
            itemControllerDic = new Dictionary<Transform, T>();
        }

        T tItem;
        if (cacheTrans.childCount == 0)
        {
            tItem = onLoadItem();
            itemControllerDic.Add(tItem.itemTransform, tItem);
        }
        else
        {
            var tItemKey = cacheTrans.GetChild(0);
            tItem = itemControllerDic[tItemKey];
        }

        tItem.itemTransform.transform.SetParent(itemTrans);
        tItem.itemTransform.transform.localPosition = Vector3.zero;
        tItem.itemTransform.transform.localEulerAngles = Vector3.zero;
        tItem.itemTransform.transform.localScale = Vector3.one;
        tItem.itemIndex = pIndex;
        return tItem;
    }

    void MoveItemToCache(T pCtrl)
    {
        if (pCtrl == null) return;
        pCtrl.itemTransform.SetParent(cacheTrans);
    }

    /// <summary>
    /// 移动所有Item到缓存池下
    /// </summary>
    void MoveAllItemToCache()
    {
        while (childCount > 0)
        {
            itemTrans.GetChild(0).SetParent(cacheTrans);
        }
        scrollView.ResetPosition();
    }

    /// <summary>
    /// 清空所有Item
    /// </summary>
    void DestroyAllItem()
    {
        if (onDeleteItem == null || itemControllerDic == null) return;
        foreach (var tCtrl in itemControllerDic.Values)
        {
            onDeleteItem(tCtrl);
        }
        itemControllerDic.Clear();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (isNoneScrollView) return;

        DestroyAllItem();
        onLoadItem = null;
        onUpdateItem = null;
        onDeleteItem = null;
    }
}

static class UIRecycleTableExtension
{
    static public void AddLocalPos(this Transform pTr, Vector2 pPos)
    {
        pTr.localPosition += new Vector3(pPos.x, pPos.y, 0);
    }
    static public void AddLocalPos(this Transform pTr, float pX, float pY)
    {
        pTr.AddLocalPos(new Vector2(pX, pY));
    }
    static public void AddLocalPosX(this Transform pTr, float pX)
    {
        pTr.AddLocalPos(pX, 0);
    }
    static public void AddLocalPosY(this Transform pTr, float pY)
    {
        pTr.AddLocalPos(0, pY);
    }

    static public void SetLocalPos(this Transform pTr, Vector2 pPos)
    {
        pTr.localPosition = new Vector3(pPos.x, pPos.y, 0);
    }
    static public void SetLocalPos(this Transform pTr, float pX, float pY)
    {
        pTr.SetLocalPos(new Vector2(pX, pY));
    }
    static public void SetLocalPosX(this Transform pTr, float pX)
    {
        pTr.SetLocalPos(pX, 0);
    }
    static public void SetLocalPosY(this Transform pTr, float pY)
    {
        pTr.SetLocalPos(0, pY);
    }
}