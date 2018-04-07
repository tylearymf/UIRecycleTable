using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// 无限滚动Table
/// --使用说明
///     ---mRecycleTable = new UIRecycleTable<ItemController>(mScrollView, OnLoadItem, OnUpdateItem, OnDeleteItem);
///        mRecycleTable.itemCount = mDatas.Count;
///        mRecycleTable.itemIntervalPixel = 20;
///        mRecycleTable.MoveToItemByIndex(10);
/// --注意
///     --当Item的边界盒有变化时，必须调用（UpdateItemBounds）更新下
/// </summary>
/// <typeparam name="T"></typeparam>
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
    public delegate T OnLoadItem(int pDataIndex);
    public delegate void OnUpdateItem(T pItemCtrl, int pDataIndex);
    public delegate void OnDeleteItem(T pItemCtrl);

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
    protected Transform cacheTrans
    {
        set;
        get;
    }
    /// <summary>
    /// 存储所有item（包含ui下的Item和cache下的item）
    /// </summary>
    protected Dictionary<Transform, T> itemControllerDic
    {
        set;
        get;
    }
    /// <summary>
    /// ui下的ItemControllers
    /// </summary>
    protected HashSet<T> itemControllers
    {
        set;
        get;
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
    /// <summary>
    /// ScrollView方向
    /// </summary>
    protected UIScrollView.Movement movement
    {
        get
        {
            if (isNoneScrollView) return UIScrollView.Movement.Custom;
            return scrollView.movement;
        }
    }
    #endregion

    #region 初始化
    /// <summary>
    /// 初始化
    /// </summary>
    protected void Init()
    {
        if (isNoneScrollView) return;
        NGUITools.DestroyChildren(scrollViewTrans);

        scrollView.restrictWithinPanel = false;
        scrollView.disableDragIfFits = false;

        if (!itemTrans)
        {
            itemTrans = NGUITools.AddChild(scrollView.gameObject).transform;
            itemTrans.name = "Items";

            switch (movement)
            {
                case UIScrollView.Movement.Horizontal:
                    itemTrans.SetLocalPos(0, panel.finalClipRegion.y);
                    break;
                case UIScrollView.Movement.Vertical:
                    itemTrans.SetLocalPos(panel.finalClipRegion.x, 0);
                    break;
            }
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

        if (itemControllers == null)
        {
            itemControllers = new HashSet<T>();
        }

        var tClipRegion = panel.finalClipRegion;
        panelBounds = new Bounds(new Vector3(tClipRegion.x, tClipRegion.y, 0), new Vector3(tClipRegion.z, tClipRegion.w, 0));

        RegisterEvent();
        MoveToItemByIndex(0);
    }

    /// <summary>
    /// 注册事件
    /// </summary>
    protected void RegisterEvent()
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
    protected void RemoveEvent()
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
    protected void AddItem(Direction pDirection)
    {
        if (pDirection == Direction.None) return;
        var tItemControllers = itemControllers;
        if (tItemControllers == null || tItemControllers.Count == 0) return;
        var tTempItemCtls = tItemControllers.OrderBy(x => x.dataIndex).ToList();
        T tTempItemCtrl = null;
        var tItemIndex = 0;
        switch (pDirection)
        {
            case Direction.Right:
            case Direction.Bottom:
                tTempItemCtrl = tTempItemCtls[tTempItemCtls.Count - 1];
                tItemIndex = tTempItemCtrl.dataIndex + 1;
                if (tItemIndex >= itemCount) return;
                break;
            case Direction.Left:
            case Direction.Top:
                tTempItemCtrl = tTempItemCtls[0];
                tItemIndex = tTempItemCtrl.dataIndex - 1;
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

        var tItemOffset = 0F;
        switch (pDirection)
        {
            case Direction.Left:
            case Direction.Bottom:
                switch (movement)
                {
                    case UIScrollView.Movement.Horizontal:
                        tItemOffset = tTempItemBounds.min.x - tItemBounds.max.x - itemIntervalPixel;
                        break;
                    case UIScrollView.Movement.Vertical:
                        tItemOffset = tTempItemBounds.min.y - tItemBounds.max.y - itemIntervalPixel;
                        break;
                }
                break;
            case Direction.Right:
            case Direction.Top:
                switch (movement)
                {
                    case UIScrollView.Movement.Horizontal:
                        tItemOffset = tTempItemBounds.max.x - tItemBounds.min.x + itemIntervalPixel;
                        break;
                    case UIScrollView.Movement.Vertical:
                        tItemOffset = tTempItemBounds.max.y - tItemBounds.min.y + itemIntervalPixel;
                        break;
                }
                break;
        }
        switch (movement)
        {
            case UIScrollView.Movement.Horizontal:
                tItemCtrl.itemTransform.SetLocalPos(tItemOffset, 0);
                break;
            case UIScrollView.Movement.Vertical:
                tItemCtrl.itemTransform.SetLocalPos(0, tItemOffset);
                break;
        }

        mCalculatedBounds = true;
    }

    /// <summary>
    /// Panel裁剪时触发
    /// </summary>
    /// <param name="pPanel"></param>
    protected void OnClipMove(UIPanel pPanel)
    {
        if (isNoneScrollView || itemCount == 0) return;
        var tPanelOffset = panel.CalculateConstrainOffset(itemsBounds.min, itemsBounds.max);

        var tIsLeft = tPanelOffset.x < -1 && movement == UIScrollView.Movement.Horizontal;
        var tIsBottom = tPanelOffset.y < -1 && movement == UIScrollView.Movement.Vertical;
        var tIsRight = tPanelOffset.x > 1 && movement == UIScrollView.Movement.Horizontal;
        var tIsTop = tPanelOffset.y > 1 && movement == UIScrollView.Movement.Vertical;

        if (tIsLeft || tIsTop)
        {
            AddItem(tIsLeft ? Direction.Left : tIsTop ? Direction.Top : Direction.None);
            var tIsTopOrLeft = itemControllers.Where(x => x.dataIndex == 0).Count() > 0;
            if (scrollView.isDragging)
            {
                immediateRestrictScrollView = tIsTopOrLeft;
                if (!tIsTopOrLeft)
                {
                    //开启这个后，当Item超出边界盒时会立即回收Item，但是有个bug，如果使劲上下拖拽，会出现Item全部消失的bug
                    //MoveOverBoundsItemToCache();
                }
            }
            else if (tIsTopOrLeft)
            {
                RestrictWithinBounds(false);
            }
        }
        else if (tIsRight || tIsBottom)
        {
            AddItem(tIsRight ? Direction.Right : tIsBottom ? Direction.Bottom : Direction.None);

            var tIsBottomOrRight = itemControllers.Where(x => x.dataIndex == itemCount - 1).Count() > 0;
            if (scrollView.isDragging)
            {
                immediateRestrictScrollView = tIsBottomOrRight;
                if (!tIsBottomOrRight)
                {
                    //开启这个后，当Item超出边界盒时会立即回收Item，但是有个bug，如果使劲上下拖拽，会出现Item全部消失的bug
                    //MoveOverBoundsItemToCache();
                }
            }
            else if (tIsBottomOrRight)
            {
                RestrictWithinBounds(false);
            }
        }
    }

    /// <summary>
    /// ScrollView停止移动时触发
    /// </summary>
    protected void OnStoppedMoving()
    {
        if (isNoneScrollView) return;
        MoveOverBoundsItemToCache();
    }

    /// <summary>
    /// ScrollView移动时触发（每帧都会触发）
    /// </summary>
    protected void OnMomentumMove()
    {
        if (isNoneScrollView) return;
    }

    /// <summary>
    /// 手指松开时触发
    /// </summary>
    protected void OnDragFinished()
    {
        if (isNoneScrollView) return;

        if (immediateRestrictScrollView)
        {
            RestrictWithinBounds(false);
        }

        if (springPanel.enabled)
        {
            springPanel.onFinished = MoveOverBoundsItemToCache;
        }
        else
        {
            MoveOverBoundsItemToCache();
        }
    }

    /// <summary>
    /// 手指按下时触发
    /// </summary>
    protected void OnDragStarted()
    {
        if (isNoneScrollView) return;
        DisableSpringPanel();
        mCalculatedBounds = true;
    }
    #endregion

    #region 其他逻辑
    /// <summary>
    /// ScrollView归位
    /// </summary>
    /// <param name="pInstant"></param>
    protected void RestrictWithinBounds(bool pInstant)
    {
        scrollView.RestrictWithinBounds(pInstant);
    }

    /// <summary>
    /// 禁掉SpringPanel的滑动
    /// </summary>
    protected void DisableSpringPanel()
    {
        springPanel.enabled = false;
    }

    /// <summary>
    /// 更新Item的边界盒（当Item的边界盒有变化时，必须调用此方法更新下）
    /// </summary>
    /// <param name="pItem"></param>
    public void UpdateItemBounds(T pItem)
    {
        var tBounds = NGUIMath.CalculateRelativeWidgetBounds(pItem.itemTransform);
        pItem.bounds = tBounds;
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
        if (movement == UIScrollView.Movement.Vertical)
        {
            var tPreviousMaxY = tPanelBounds.max.y;
            while (tPreviousMaxY > tPanelBounds.min.y)
            {
                if (pIndex >= itemCount - 1) break;
                var tItemCtrl = GetItemControllerByCache(pIndex);
                if (onUpdateItem != null) onUpdateItem(tItemCtrl, pIndex++);
                var tItemBounds = NGUIMath.CalculateRelativeWidgetBounds(tItemCtrl.itemTransform);
                tItemCtrl.bounds = tItemBounds;

                var tItemOffsetY = tPreviousMaxY - tItemBounds.max.y;
                tItemCtrl.itemTransform.SetLocalPos(0, tItemOffsetY);
                tPreviousMaxY = tPreviousMaxY - tItemBounds.size.y - itemIntervalPixel;
            }
        }
        else if (movement == UIScrollView.Movement.Horizontal)
        {
            var tPreviousMinX = tPanelBounds.min.x;
            while (tPreviousMinX < tPanelBounds.max.x)
            {
                if (pIndex >= itemCount - 1) break;
                var tItemCtrl = GetItemControllerByCache(pIndex);
                if (onUpdateItem != null) onUpdateItem(tItemCtrl, pIndex++);
                var tItemBounds = NGUIMath.CalculateRelativeWidgetBounds(tItemCtrl.itemTransform);
                tItemCtrl.bounds = tItemBounds;

                var tItemOffsetX = tPreviousMinX - tItemBounds.min.x;
                tItemCtrl.itemTransform.SetLocalPos(tItemOffsetX, 0);
                tPreviousMinX = tPreviousMinX + tItemBounds.size.x + itemIntervalPixel;
            }
        }
    }

    /// <summary>
    /// ScrollView归位
    /// </summary>
    public void ResetPosition()
    {
        MoveToItemByIndex(0);
    }
    #endregion

    #region 缓存池
    /// <summary>
    /// 移除超出区域的Item到缓存池
    /// </summary>
    protected void MoveOverBoundsItemToCache()
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
    protected T GetItemControllerByCache(int pIndex)
    {
        if (itemControllerDic == null)
        {
            itemControllerDic = new Dictionary<Transform, T>();
        }

        T tItem = null;
        if (cacheTrans.childCount == 0)
        {
            tItem = onLoadItem(pIndex);
            itemControllerDic.Add(tItem.itemTransform, tItem);
        }
        else
        {
            tItem = itemControllerDic[cacheTrans.GetChild(0)];
        }

        MoveItemToUI(tItem);
        tItem.dataIndex = pIndex;
        return tItem;
    }

    /// <summary>
    /// 移动Item到UI界面
    /// </summary>
    /// <param name="pItem"></param>
    protected void MoveItemToUI(T pItem)
    {
        if (pItem == null) return;
        var tTransform = pItem.itemTransform;
        tTransform.SetParent(itemTrans);
        tTransform.transform.localPosition = Vector3.zero;
        tTransform.transform.localEulerAngles = Vector3.zero;
        tTransform.transform.localScale = Vector3.one;
        itemControllers.Add(pItem);
    }

    /// <summary>
    /// 移动指定Item到缓存池
    /// </summary>
    /// <param name="pItem"></param>
    protected void MoveItemToCache(T pItem)
    {
        if (pItem == null) return;
        pItem.itemTransform.SetParent(cacheTrans);
        itemControllers.Remove(pItem);
    }

    /// <summary>
    /// 移动所有Item到缓存池下
    /// </summary>
    protected void MoveAllItemToCache()
    {
        while (childCount > 0)
        {
            var tItem = itemControllerDic[itemTrans.GetChild(0)];
            MoveItemToCache(tItem);
        }
        panel.baseClipRegion = new Vector4(panelBounds.center.x, panelBounds.center.y, panelBounds.size.x, panelBounds.size.y);
    }
    #endregion

    #region 释放资源
    /// <summary>
    /// 清空所有Item
    /// </summary>
    protected void DestroyAllItem()
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
    /// <summary>
    /// 数据下标（自定义的Controller里面不能修改该属性）
    /// </summary>
    int dataIndex { set; get; }
    /// <summary>
    /// 返回该Item的Transform
    /// </summary>
    Transform itemTransform { get; }
    /// <summary>
    /// Item的边界盒（自定义的Controller里面不能修改该属性）
    /// </summary>
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

    static public void SetLocalPos(this Transform pTr, Vector2 pPos)
    {
        pTr.localPosition = new Vector3(pPos.x, pPos.y, pTr.localPosition.z);
    }
    static public void SetLocalPos(this Transform pTr, float pX, float pY)
    {
        pTr.SetLocalPos(new Vector2(pX, pY));
    }
}