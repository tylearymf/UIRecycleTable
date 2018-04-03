using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public class UIRecycleTable<T> : IDisposable where T : class, IRecycleTable
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
    protected Transform scrollViewTrans
    {
        set;
        get;
    }
    protected Transform itemTrans
    {
        set;
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
            return itemControllerDic.Values.ToList().FindAll(x => x.itemTransform.gameObject.activeInHierarchy);
        }
    }
    /// <summary>
    /// 是否有ScrollView
    /// </summary>
    protected bool isNoneScrollView
    {
        get { return !scrollView; }
    }
    /// <summary>
    /// 是否有Item
    /// </summary>
    protected bool isNoneChild
    {
        get { return !itemTrans || itemTrans.childCount == 0; }
    }
    protected int childCount
    {
        get { return isNoneChild ? 0 : itemTrans.childCount; }
    }
    protected Bounds itemsBounds
    {
        get
        {
            if (mCalculatedBounds)
            {
                mCalculatedBounds = false;
                mItemsBounds = NGUIMath.CalculateRelativeWidgetBounds(itemTrans);
            }
            return mItemsBounds;
        }
    }
    protected Bounds panelBounds
    {
        set;
        get;
    }
    protected SpringPanel springPanel
    {
        set;
        get;
    }
    /// <summary>
    /// 是否立即归位ScrollView
    /// </summary>
    protected bool immediateRestrictScrollView
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

        scrollView.restrictWithinPanel = false;
        scrollView.disableDragIfFits = false;

        if (!itemTrans)
        {
            itemTrans = NGUITools.AddChild(scrollView.gameObject).transform;
            itemTrans.name = "Items";
        }

        if (!cacheTrans)
        {
            cacheTrans = NGUITools.AddChild(scrollViewTrans.parent.gameObject).transform;
            cacheTrans.name = "UIRecycleTableCache";
            cacheTrans.gameObject.SetActive(false);
        }

        if (!springPanel)
        {
            springPanel = scrollViewTrans.gameObject.AddMissingComponent<SpringPanel>();
            springPanel.enabled = false;
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
    /// 添加Item
    /// </summary>
    /// <param name="pDirection"></param>
    void AddItem(Direction pDirection)
    {
        var tItemControllers = itemControllers;
        if (tItemControllers == null || tItemControllers.Count == 0) return;
        tItemControllers.Sort((x, y) => x.itemIndex.CompareTo(y.itemIndex));
        T tTempItemCtrl = null;
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

        var tTempItemBounds = NGUIMath.CalculateRelativeWidgetBounds(tTempItemCtrl.itemTransform);
        tTempItemBounds.center += tTempItemCtrl.itemTransform.localPosition;

        var tItemBounds = NGUIMath.CalculateRelativeWidgetBounds(tItemCtrl.itemTransform);
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
        tItemCtrl.itemTransform.SetLocalPosY(tItemOffsetY);

        mCalculatedBounds = true;
    }

    /// <summary>
    /// Panel裁剪时触发
    /// </summary>
    /// <param name="pPanel"></param>
    private void OnClipMove(UIPanel pPanel)
    {
        if (isNoneScrollView || itemCount == 0) return;

        var tPanelOffset = panel.CalculateConstrainOffset(itemsBounds.min, itemsBounds.max);
        if (tPanelOffset.y > 1)
        {
            AddItem(Direction.Top);
            var tIsTop = itemControllers.Exists(x => x.itemIndex == 0);
            if (scrollView.isDragging)
            {
                immediateRestrictScrollView = tIsTop;
                if (!tIsTop)
                {
                    MoveOverBoundsItemTOCache();
                }
            }
            else if (tIsTop)
            {
                RestrictWithinBounds(false);
            }
        }
        else if (tPanelOffset.y < -1)
        {
            AddItem(Direction.Bottom);

            var tIsBottom = itemControllers.Exists(x => x.itemIndex == itemCount - 1);
            if (scrollView.isDragging)
            {
                immediateRestrictScrollView = tIsBottom;
                if (!tIsBottom)
                {
                    MoveOverBoundsItemTOCache();
                }
            }
            else if (tIsBottom)
            {
                RestrictWithinBounds(false);
            }
        }
    }

    /// <summary>
    /// ScrollView停止移动时触发
    /// </summary>
    private void OnStoppedMoving()
    {
        if (isNoneScrollView) return;
        MoveOverBoundsItemTOCache();
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

        if (immediateRestrictScrollView)
        {
            RestrictWithinBounds(false);
        }

        if (springPanel.enabled)
        {
            springPanel.onFinished = MoveOverBoundsItemTOCache;
        }
        else
        {
            MoveOverBoundsItemTOCache();
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

    #region 其他逻辑
    /// <summary>
    /// ScrollView归位
    /// </summary>
    /// <param name="pInstant"></param>
    void RestrictWithinBounds(bool pInstant)
    {
        scrollView.RestrictWithinBounds(pInstant);
    }

    /// <summary>
    /// 禁掉SpringPanel的滑动
    /// </summary>
    void DisableSpringPanel()
    {
        springPanel.enabled = false;
    }
    #endregion

    #region 定位
    /// <summary>
    /// 定位到指定Index下
    /// </summary>
    /// <param name="pIndex"></param>
    public void MoveToItemByIndex(int pIndex)
    {
        if (isNoneScrollView) return;

        MoveAllItemToCache();

        if (itemCount == 0) return;
        var tPanelBounds = panelBounds;
        var tPreviousMaxY = tPanelBounds.max.y;
        while (tPreviousMaxY > tPanelBounds.min.y)
        {
            if (pIndex >= itemCount - 1) break;
            var tItemCtrl = GetItemControllerByCache(pIndex);
            if (onUpdateItem != null) onUpdateItem(tItemCtrl, pIndex++);
            var tItemBounds = NGUIMath.CalculateRelativeWidgetBounds(tItemCtrl.itemTransform);
            tItemCtrl.bounds = tItemBounds;

            var tItemOffsetY = tPreviousMaxY - tItemBounds.max.y;
            tItemCtrl.itemTransform.SetLocalPosY(tItemOffsetY);
            tPreviousMaxY = tPreviousMaxY - tItemBounds.size.y - itemIntervalPixel;
        }
    }
    #endregion

    #region 缓存池
    /// <summary>
    /// 移除超出区域的Item到缓存池
    /// </summary>
    void MoveOverBoundsItemTOCache()
    {
        for (int i = 0; i < childCount;)
        {
            var tKey = itemTrans.GetChild(i);
            T tCtrl = default(T);
            if (!itemControllerDic.TryGetValue(tKey, out tCtrl)) continue;
            var tItemBounds = tCtrl.bounds;
            tItemBounds.center += tCtrl.itemTransform.localPosition + scrollViewTrans.localPosition;
            if (panelBounds.Intersects(tItemBounds))
            {
                ++i;
                continue;
            }
            MoveItemToCache(tCtrl);
        }
        mCalculatedBounds = true;
    }

    /// <summary>
    /// 从缓存池获取Item
    /// </summary>
    /// <param name="pIndex"></param>
    /// <returns></returns>
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

    /// <summary>
    /// 移动指定Item到缓存池
    /// </summary>
    /// <param name="pCtrl"></param>
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
        panel.baseClipRegion = new Vector4(panelBounds.center.x, panelBounds.center.y, panelBounds.size.x, panelBounds.size.y);
    }
    #endregion

    #region 释放资源
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
    #endregion
}

/// <summary>
/// Item 必须继承自该接口
/// </summary>
public interface IRecycleTable
{
    int itemIndex { set; get; }
    Transform itemTransform { get; }
    Bounds bounds { set; get; }
}

/// <summary>
/// 扩展
/// </summary>
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