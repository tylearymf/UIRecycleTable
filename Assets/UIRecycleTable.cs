﻿using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// UIRecycleTable
/// --Prefab使用说明
///     1、ScrollView不需要有子物体，ScrollView不需要有子物体，ScrollView不需要有子物体（本脚本会在Init的时候清除所有ScrollView的子物体）
///     
/// --代码使用说明
///     1、使用构造方法初始化UIRecycleTable，例如：mRecycleTable = new UIRecycleTable<ItemController>(mScrollView, OnLoadItem, OnUpdateItem, OnDeleteItem);
///     2、当Item数量有变化时，直接调用itemCount属性赋值，或者调用ResetPosition、ForceRefreshItem方法
///     3、当界面中的某个Item的宽高有变化的时候，调用ForceFefreshItem重新刷新下界面
///     4、在界面Dispose时，必须调用本脚本的Dispose释放资源
/// 
///     注意：ItemController 必须继承IRecycleTable接口，具体的看IRecycleTable注释
/// 
/// --公共的委托、属性、方法
///     1、onLoadItem 当缓存池里面没有Item时，调用绑定的委托加载Prefab
///     2、onUpdateItem 刷新Item
///     3、onDeleteItem 在Dispose时，清理所有Item
///     4、onEndTarget、onStartTarget，用法具体查看注释
///     5、getPrefabType 当有多种Prefab类型的时候必须绑定该委托，然后在ItemController初始化时为IRecycleTable接口中的prefabType赋值
///     
///     5、itemCount 设置Item的数量
///     6、itemIntervalPixel 设置Item间距
///     7、itemTrans 在onLoadItem里面加载出来的预制放在这个物体下
///     
///     8、MoveToItemByIndex 定位到某个Item下，传入dataIndex
///     9、ResetPosition 重置ScrollView位置（就是定位到第0个Item）
///     10、ForceRefreshItem 用法具体查看注释
/// 
/// </summary>
/// <typeparam name="T"></typeparam>
public class UIRecycleTable<T> : IDisposable where T : class, IRecycleTable
{
    public enum Direction
    {
        None = 0,
        Left = 1 << 0,
        Bottom = 1 << 1,
        Right = 1 << 2,
        Top = 1 << 3,
    }

    #region 构造方法
    protected UIRecycleTable() { }

    /// <summary>
    /// UIRecycleTable唯一入口
    /// </summary>
    /// <param name="pScrollView"></param>
    /// <param name="pOnLoadItem">加载item</param>
    /// <param name="pOnUpdateItem">更新item</param>
    /// <param name="pOnDeleteItem">删除item</param>
    /// <param name="pGetPrefabType">当有多种Prefab类型的时候必须绑定该委托，然后在ItemController初始化时为IRecycleTable接口中的prefabType赋值</param>
    public UIRecycleTable(UIScrollView pScrollView, OnLoadItem pOnLoadItem, OnUpdateItem pOnUpdateItem, OnDeleteItem pOnDeleteItem, GetPrefabType pGetPrefabType)
    {
        if (pScrollView == null) return;

        scrollView = pScrollView;
        panel = scrollView.panel;
        scrollViewTrans = scrollView.transform;
        onLoadItem = pOnLoadItem;
        onUpdateItem = pOnUpdateItem;
        onDeleteItem = pOnDeleteItem;
        getPrefabType = pGetPrefabType;

        Init();
    }
    #endregion

    #region 委托
    public delegate T OnLoadItem(int pDataIndex);
    public delegate int GetPrefabType(int pDataIndex);
    public delegate void OnUpdateItem(T pItemCtrl, int pDataIndex);
    public delegate void OnDeleteItem(T pItemCtrl);
    public delegate void OnTriggerTarget(float pOffset);

    /// <summary>
    /// 加载Item
    /// </summary>
    public OnLoadItem onLoadItem
    {
        protected set;
        get;
    }
    /// <summary>
    /// 获取Prefab类型
    /// </summary>
    public GetPrefabType getPrefabType
    {
        protected set;
        get;
    }
    /// <summary>
    /// 刷新Item
    /// </summary>
    public OnUpdateItem onUpdateItem
    {
        protected set;
        get;
    }
    /// <summary>
    /// 删除Item
    /// </summary>
    public OnDeleteItem onDeleteItem
    {
        protected set;
        get;
    }
    /// <summary>
    /// 当向上或向左滑动，ScrollView触底回弹时触发该委托 参数值为Panel偏移量（此委托会频繁触发，如果不需要在短时间内触发多次，可以以绑定个倒计时，多少秒后再触发）
    /// </summary>
    public OnTriggerTarget onEndTarget
    {
        set;
        get;
    }
    /// <summary>
    /// 当向下或向右滑动，ScrollView触顶回弹时触发该委托 参数值为Panel偏移量（此委托会频繁触发，如果不需要在短时间内触发多次，可以以绑定个倒计时，多少秒后再触发）
    /// </summary>
    public OnTriggerTarget onStartTarget
    {
        set;
        get;
    }
    #endregion

    #region 字段
    protected int mItemMaxCount;
    protected Bounds mItemsBounds;
    protected bool mCalculatedBounds;
    protected Coroutine mWheelCorutine;
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
    /// <summary>
    /// 生成出来的预制放在这个物体下
    /// </summary>
    public Transform itemTrans
    {
        protected set;
        get;
    }
    protected Transform scrollViewTrans
    {
        set;
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
        get { return !itemTrans || itemControllers == null || itemControllers.Count == 0; }
    }
    protected int childCount
    {
        get { return isNoneChild ? 0 : itemControllers.Count; }
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
    /// 拖拽信息
    /// </summary>
    protected RecycleTableDragInfo<T> dragInfo
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
    /// <summary>
    /// 获取滑动方向
    /// </summary>
    protected Direction dragDirection
    {
        get
        {
            if (isNoneScrollView) return Direction.None;
            switch (movement)
            {
                case UIScrollView.Movement.Horizontal:
                    if (scrollView.currentMomentum.x == 0) return Direction.None;
                    return scrollView.currentMomentum.x > 0 ? Direction.Right : Direction.Left;
                case UIScrollView.Movement.Vertical:
                    if (scrollView.currentMomentum.y == 0) return Direction.None;
                    return scrollView.currentMomentum.y > 0 ? Direction.Top : Direction.Bottom;
                default:
                    return Direction.None;
            }
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

        var tClipRegion = panel.finalClipRegion;
        var tCenter = new Vector3(tClipRegion.x, tClipRegion.y, 0);
        tCenter += scrollViewTrans.localPosition;
        panelBounds = new Bounds(new Vector3(tCenter.x, tCenter.y, 0), new Vector3(tClipRegion.z, tClipRegion.w, 0));
        scrollViewTrans.localPosition = Vector3.zero;
        panel.clipOffset = Vector2.zero;
        panel.baseClipRegion = new Vector4(panelBounds.center.x, panelBounds.center.y, panelBounds.size.x, panelBounds.size.y);

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
            var tCacheTrans = scrollViewTrans.parent.Find("UIRecycleTableCache");
            if (tCacheTrans != null)
            {
                cacheTrans = tCacheTrans;
            }
            else
            {
                cacheTrans = NGUITools.AddChild(scrollViewTrans.parent.gameObject).transform;
                cacheTrans.name = "UIRecycleTableCache";
            }
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

        dragInfo = new RecycleTableDragInfo<T>()
        {
            restrictScrollView = true,
            instant = false,
            dragDirection = Direction.None,
            movement = movement,
            panelOffset = Vector2.zero,
        };

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
        scrollView.onScrollWheel += OnScrollWheel;
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
        scrollView.onScrollWheel -= OnScrollWheel;
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
        if (itemControllers == null || itemControllers.Count == 0) return;
        T tTempItemCtrl = null;
        var tDataIndex = 0;
        switch (pDirection)
        {
            case Direction.Right:
            case Direction.Bottom:
                tTempItemCtrl = UIRecycleTableExtension.FindCustom<T, float>(itemControllers, x => x.recycleTablInfo.dataIndex, false);
                tDataIndex = tTempItemCtrl.recycleTablInfo.dataIndex + 1;
                if (tDataIndex >= itemCount) return;
                break;
            case Direction.Left:
            case Direction.Top:
                tTempItemCtrl = UIRecycleTableExtension.FindCustom<T, float>(itemControllers, x => x.recycleTablInfo.dataIndex, true);
                tDataIndex = tTempItemCtrl.recycleTablInfo.dataIndex - 1;
                if (tDataIndex < 0) return;
                break;
        }
        if (tTempItemCtrl == null) return;
        var tItemCtrl = GetItemControllerByCache(tDataIndex);
        onUpdateItem(tItemCtrl, tDataIndex);

        var tTempItemBounds = NGUIMath.CalculateRelativeWidgetBounds(tTempItemCtrl.itemTransform);
        tTempItemBounds.center += tTempItemCtrl.itemTransform.localPosition;

        var tItemBounds = NGUIMath.CalculateRelativeWidgetBounds(tItemCtrl.itemTransform);
        var tInfo = tItemCtrl.recycleTablInfo;
        tInfo.bounds = tItemBounds;
        tItemCtrl.recycleTablInfo = tInfo;

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

        var tPanelOffset = Vector3.zero;
        var tAddItemDirection = CalculateAddItemDirection(out tPanelOffset);
        var tIsLeft = tAddItemDirection == Direction.Left;
        var tIsBottom = tAddItemDirection == Direction.Bottom;
        var tIsRight = tAddItemDirection == Direction.Right;
        var tIsTop = tAddItemDirection == Direction.Top;

        if (tIsLeft || tIsTop)
        {
            AddItem(tIsLeft ? Direction.Left : tIsTop ? Direction.Top : Direction.None);

            var tIsTopOrLeft = false;
            var tFirstItemCtrl = UIRecycleTableExtension.Find(itemControllers, x => x.recycleTablInfo.dataIndex == 0);
            var tFirstBounds = new Bounds();
            if (tFirstItemCtrl != null)
            {
                tFirstBounds = tFirstItemCtrl.recycleTablInfo.bounds;
                tFirstBounds.center += tFirstItemCtrl.itemTransform.localPosition + scrollViewTrans.localPosition;
                tIsTopOrLeft = tIsLeft ? tFirstBounds.min.x > panelBounds.min.x : tIsTop ? tFirstBounds.max.y < panelBounds.max.y : tIsTopOrLeft;
            }

            var tInfo = new RecycleTableDragInfo<T>()
            {
                restrictScrollView = tIsTopOrLeft,
                panelOffset = tPanelOffset,
                dragDirection = Direction.Top | Direction.Left,
                instant = false,
                movement = movement,
            };

            if (scrollView.isDragging)
            {
                dragInfo = tInfo;
                if (!tIsTopOrLeft)
                {
                    MoveOverBoundsItemToCache();
                }
            }
            else if (tIsTopOrLeft)
            {
                RestrictWithinBounds(tInfo);
            }
            else
            {
                MoveOverBoundsItemToCache(false);
            }
        }
        else if (tIsRight || tIsBottom)
        {
            AddItem(tIsRight ? Direction.Right : tIsBottom ? Direction.Bottom : Direction.None);

            var tIsBottomOrRight = false;
            var tLastItemCtrl = UIRecycleTableExtension.Find(itemControllers, x => x.recycleTablInfo.dataIndex == itemCount - 1);
            var tLastBounds = new Bounds();
            if (tLastItemCtrl != null)
            {
                tLastBounds = tLastItemCtrl.recycleTablInfo.bounds;
                tLastBounds.center += tLastItemCtrl.itemTransform.localPosition + scrollViewTrans.localPosition;
                tIsBottomOrRight = tIsRight ? tLastBounds.max.x < panelBounds.max.x : tIsBottom ? tLastBounds.min.y > panelBounds.min.y : tIsBottomOrRight;
            }

            var tInfo = new RecycleTableDragInfo<T>()
            {
                restrictScrollView = tIsBottomOrRight,
                panelOffset = tPanelOffset,
                dragDirection = Direction.Bottom | Direction.Right,
                instant = false,
                movement = movement,
            };

            if (scrollView.isDragging)
            {
                dragInfo = tInfo;
                if (!tIsBottomOrRight)
                {
                    MoveOverBoundsItemToCache();
                }
            }
            else if (tIsBottomOrRight)
            {
                RestrictWithinBounds(tInfo);
            }
            else
            {
                MoveOverBoundsItemToCache(false);
            }
        }
    }

    /// <summary>
    /// 在返回的方向上添加Item
    /// </summary>
    /// <returns></returns>
    Direction CalculateAddItemDirection(out Vector3 pOffset)
    {
        pOffset = Vector3.zero;
        if (dragDirection == Direction.None) return Direction.None;
        var tItemBounds = itemsBounds;
        tItemBounds.center += itemTrans.localPosition + scrollViewTrans.localPosition;

        switch (dragDirection)
        {
            case Direction.Left:
                pOffset = new Vector3(tItemBounds.max.x - panelBounds.max.x, 0, 0);
                if (tItemBounds.max.x <= panelBounds.max.x) return Direction.Right;
                break;
            case Direction.Bottom:
                pOffset = new Vector3(0, tItemBounds.max.y - panelBounds.max.y, 0);
                if (tItemBounds.max.y <= panelBounds.max.y) return Direction.Top;
                break;
            case Direction.Right:
                pOffset = new Vector3(tItemBounds.min.x - panelBounds.min.x, 0, 0);
                if (tItemBounds.min.x >= panelBounds.min.x) return Direction.Left;
                break;
            case Direction.Top:
                pOffset = new Vector3(0, tItemBounds.min.y - panelBounds.min.y, 0);
                if (tItemBounds.min.y >= panelBounds.min.y) return Direction.Bottom;
                break;
        }
        return Direction.None;
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

        RestrictWithinBounds(dragInfo);

        if (springPanel.enabled)
        {
            springPanel.onFinished = () => { MoveOverBoundsItemToCache(); };
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

    /// <summary>
    /// 鼠标滚轮旋转时触发
    /// </summary>
    protected void OnScrollWheel()
    {
        if (ScrollViewHasSpace())
        {
            if (mWheelCorutine != null) scrollView.StopCoroutine(mWheelCorutine);
            if (!scrollView.gameObject.activeInHierarchy) return;
            mWheelCorutine = scrollView.StartCoroutine(waitForFrame());
        }
    }

    /// <summary>
    /// 由于鼠标滚轮的滑动是在LateUpdate里面执行的，所以要延迟到下一帧来处理滚轮的逻辑
    /// </summary>
    /// <returns></returns>
    System.Collections.IEnumerator waitForFrame()
    {
        yield return null;
        DisableSpringPanel();
        RestrictWithinBounds(dragInfo);
    }
    #endregion

    #region 其他逻辑
    /// <summary>
    /// ScrollView是否有空白区域
    /// </summary>
    /// <returns></returns>
    protected bool ScrollViewHasSpace()
    {
        var tHasSpace = movement == UIScrollView.Movement.Vertical ? itemsBounds.size.y < panelBounds.size.y :
               movement == UIScrollView.Movement.Horizontal ? itemsBounds.size.x < panelBounds.size.x : false;
        return childCount == itemCount && tHasSpace;
    }

    /// <summary>
    /// 当Item未填满ScrollView时，限制在Top/Left
    /// </summary>
    /// <param name="pDragInfo"></param>
    /// <returns></returns>
    protected bool RestrictIfFits(ref RecycleTableDragInfo<T> pDragInfo)
    {
        if (!ScrollViewHasSpace()) return false;

        var tConstraint = Vector3.zero;
        var b = itemsBounds;
        b.center += itemTrans.localPosition + scrollViewTrans.localPosition;

        switch (movement)
        {
            case UIScrollView.Movement.Horizontal:
                var tMin = panelBounds.min.x - b.min.x;
                tConstraint = new Vector3(tMin + panel.clipSoftness.x, 0, 0);

                pDragInfo.dragDirection = tConstraint.x < 0 ? Direction.Left : tConstraint.x > 0 ? Direction.Right : Direction.None;
                break;
            case UIScrollView.Movement.Vertical:
                var tMax = panelBounds.max.y - b.max.y;
                tConstraint = new Vector3(0, tMax - panel.clipSoftness.y, 0);

                pDragInfo.dragDirection = tConstraint.y > 0 ? Direction.Top : tConstraint.y < 0 ? Direction.Bottom : Direction.None;
                break;
        }

        if (tConstraint.sqrMagnitude > 0.1F)
        {
            if (!pDragInfo.instant && scrollView.dragEffect == UIScrollView.DragEffect.MomentumAndSpring)
            {
                var tPos = scrollViewTrans.localPosition + tConstraint;
                tPos.x = Mathf.Round(tPos.x);
                tPos.y = Mathf.Round(tPos.y);
                SpringPanel.Begin(scrollViewTrans.gameObject, tPos, 8F);
            }
            else
            {
                scrollView.MoveRelative(tConstraint);
                var tMomentum = scrollView.currentMomentum;
                if (Mathf.Abs(tConstraint.x) > 0.01F) tMomentum.x = 0;
                if (Mathf.Abs(tConstraint.y) > 0.01F) tMomentum.y = 0;
                if (Mathf.Abs(tConstraint.z) > 0.01F) tMomentum.z = 0;
                scrollView.currentMomentum = tMomentum;
            }
        }
        return true;
    }

    /// <summary>
    /// ScrollView归位
    /// </summary>
    /// <param name="pInstant"></param>
    protected void RestrictWithinBounds(RecycleTableDragInfo<T> pDragInfo)
    {
        if (!pDragInfo.restrictScrollView) return;

        if (!RestrictIfFits(ref pDragInfo))
        {
            scrollView.RestrictWithinBounds(pDragInfo.instant);
        }

        if (pDragInfo.isTopOrLeft && onStartTarget != null)
        {
            onStartTarget(pDragInfo.offset);
        }

        if (pDragInfo.isBottomOrRight && onEndTarget != null)
        {
            onEndTarget(pDragInfo.offset);
        }
    }

    /// <summary>
    /// 禁掉SpringPanel的滑动
    /// </summary>
    public void DisableSpringPanel() { springPanel.enabled = false; }

    public void CalculatedBounds() { mCalculatedBounds = true; }
    #endregion

    #region 定位
    /// <summary>
    /// 定位到指定Index下
    /// </summary>
    /// <param name="pDataIndex"></param>
    public void MoveToItemByIndex(int pDataIndex)
    {
        if (isNoneScrollView) return;
        DisableSpringPanel();
        MoveAllItemToCache();
        if (itemCount == 0) return;
        moveToItemByIndex(pDataIndex, panelBounds);
    }
    void moveToItemByIndex(int pDataIndex, Bounds pFirstBounds)
    {
        var tPanelBounds = panelBounds;
        if (movement == UIScrollView.Movement.Vertical)
        {
            var tPreviousMaxY = pFirstBounds.max.y;
            while (tPreviousMaxY > tPanelBounds.min.y)
            {
                if (pDataIndex >= itemCount) break;
                var tItemCtrl = GetItemControllerByCache(pDataIndex);
                if (onUpdateItem != null) onUpdateItem(tItemCtrl, pDataIndex++);
                var tItemBounds = NGUIMath.CalculateRelativeWidgetBounds(tItemCtrl.itemTransform);
                var tInfo = tItemCtrl.recycleTablInfo;
                tInfo.bounds = tItemBounds;
                tItemCtrl.recycleTablInfo = tInfo;

                var tItemOffsetY = tPreviousMaxY - tItemBounds.max.y;
                tItemCtrl.itemTransform.SetLocalPos(0, tItemOffsetY);
                tPreviousMaxY = tPreviousMaxY - tItemBounds.size.y - itemIntervalPixel;
            }
        }
        else if (movement == UIScrollView.Movement.Horizontal)
        {
            var tPreviousMinX = pFirstBounds.min.x;
            while (tPreviousMinX < tPanelBounds.max.x)
            {
                if (pDataIndex >= itemCount) break;
                var tItemCtrl = GetItemControllerByCache(pDataIndex);
                if (onUpdateItem != null) onUpdateItem(tItemCtrl, pDataIndex++);
                var tItemBounds = NGUIMath.CalculateRelativeWidgetBounds(tItemCtrl.itemTransform);
                var tInfo = tItemCtrl.recycleTablInfo;
                tInfo.bounds = tItemBounds;
                tItemCtrl.recycleTablInfo = tInfo;

                var tItemOffsetX = tPreviousMinX - tItemBounds.min.x;
                tItemCtrl.itemTransform.SetLocalPos(tItemOffsetX, 0);
                tPreviousMinX = tPreviousMinX + tItemBounds.size.x + itemIntervalPixel;
            }
        }
    }

    /// <summary>
    /// ScrollView归位
    /// </summary>
    public void ResetPosition(int pItemCount = -1)
    {
        if (isNoneScrollView) return;
        if (pItemCount != -1) itemCount = pItemCount;
        MoveToItemByIndex(0);
    }

    /// <summary>
    /// 强制重新刷新界面Item,保持原来的Item排版（如果Item数量有变更，必须传入Item数量）
    /// </summary>
    /// <param name="pItemCount"></param>
    public void ForceRefreshItem(int pItemCount = -1)
    {
        if (isNoneScrollView) return;
        if (pItemCount != -1) itemCount = pItemCount;
        if (childCount == 0)
        {
            MoveToItemByIndex(0);
            return;
        }
        DisableSpringPanel();
        MoveOverBoundsItemToCache();
        T tFirst = null;
        switch (movement)
        {
            case UIScrollView.Movement.Horizontal:
                tFirst = UIRecycleTableExtension.FindCustom<T, float>(itemControllers, x => x.itemTransform.localPosition.x, true);
                break;
            case UIScrollView.Movement.Vertical:
                tFirst = UIRecycleTableExtension.FindCustom<T, float>(itemControllers, x => x.itemTransform.localPosition.y, false);
                break;
        }
        if (tFirst == null) return;
        MoveAllItemToCache();
        var tBounds = tFirst.recycleTablInfo.bounds;
        tBounds.center += tFirst.itemTransform.localPosition;
        moveToItemByIndex(tFirst.recycleTablInfo.dataIndex, tBounds);
    }
    #endregion

    #region 缓存池
    /// <summary>
    /// 移除超出区域的Item到缓存池
    /// </summary>
    protected void MoveOverBoundsItemToCache(bool pCalculateBounds = true)
    {
        for (int i = 0; i < childCount;)
        {
            if (itemTrans.childCount == 1 || i >= itemTrans.childCount) break;
            var tKey = itemTrans.GetChild(i);
            T tCtrl = default(T);
            if (!itemControllerDic.TryGetValue(tKey, out tCtrl)) continue;
            var tItemBounds = tCtrl.recycleTablInfo.bounds;
            tItemBounds.center += tCtrl.itemTransform.localPosition + scrollViewTrans.localPosition;
            if (panelBounds.Intersects(tItemBounds))
            {
                ++i;
                continue;
            }
            MoveItemToCache(tCtrl);
        }
        if (pCalculateBounds) CalculatedBounds();
    }

    /// <summary>
    /// 从缓存池获取Item
    /// </summary>
    /// <param name="pDataIndex"></param>
    /// <returns></returns>
    protected T GetItemControllerByCache(int pDataIndex)
    {
        if (itemControllerDic == null)
        {
            itemControllerDic = new Dictionary<Transform, T>();
        }

        T tItemCtrl = null;
        if (cacheTrans.childCount > 0)
        {
            if (getPrefabType == null)
            {
                tItemCtrl = itemControllerDic[cacheTrans.GetChild(0)];
            }
            else
            {
                for (int i = 0; i < cacheTrans.childCount; i++)
                {
                    var tCtrl = itemControllerDic[cacheTrans.GetChild(i)];
                    if (getPrefabType(pDataIndex) == tCtrl.prefabType)
                    {
                        tItemCtrl = tCtrl;
                        break;
                    }
                }
            }
        }

        if (tItemCtrl == null)
        {
            tItemCtrl = onLoadItem(pDataIndex);
            itemControllerDic.Add(tItemCtrl.itemTransform, tItemCtrl);
        }

        MoveItemToUI(tItemCtrl);
        var tInfo = tItemCtrl.recycleTablInfo;
        tInfo.dataIndex = pDataIndex;
        tItemCtrl.recycleTablInfo = tInfo;
        return tItemCtrl;
    }

    /// <summary>
    /// 移动Item到UI界面
    /// </summary>
    /// <param name="pItemCtrl"></param>
    protected void MoveItemToUI(T pItemCtrl)
    {
        if (pItemCtrl == null) return;
        var tTransform = pItemCtrl.itemTransform;
        tTransform.SetParent(itemTrans);
        tTransform.transform.localPosition = Vector3.zero;
        tTransform.transform.localEulerAngles = Vector3.zero;
        tTransform.transform.localScale = Vector3.one;
        itemControllers.Add(pItemCtrl);
    }

    /// <summary>
    /// 移动指定Item到缓存池
    /// </summary>
    /// <param name="pItemCtrl"></param>
    protected void MoveItemToCache(T pItemCtrl)
    {
        if (pItemCtrl == null) return;
        pItemCtrl.itemTransform.SetParent(cacheTrans);
        itemControllers.Remove(pItemCtrl);
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

        panel.clipOffset = Vector2.zero;
        scrollViewTrans.localPosition = Vector3.zero;
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

        RemoveEvent();
        DestroyAllItem();
        onLoadItem = null;
        onUpdateItem = null;
        onDeleteItem = null;
        onEndTarget = null;
        onStartTarget = null;
        getPrefabType = null;
    }
    #endregion
}

/// <summary>
/// Item 必须继承自该接口
/// </summary>
public interface IRecycleTable
{
    /// <summary>
    /// 自定义的Controller里面不能修改该类的属性
    /// </summary>
    RecycleTableInfo recycleTablInfo { set; get; }
    /// <summary>
    /// Prefab类型（用于存在类型不同的Prefab）
    /// </summary>
    int prefabType { set; get; }
    /// <summary>
    /// 返回该Item的Transform
    /// </summary>
    Transform itemTransform { get; }
}

/// <summary>
/// 自定义的Controller里面不能修改该类的属性
/// </summary>
public struct RecycleTableInfo
{
    /// <summary>
    /// 数据下标
    /// </summary>
    public int dataIndex { set; get; }
    /// <summary>
    ///  Item的边界盒
    /// </summary>
    public Bounds bounds { set; get; }
}

public struct RecycleTableDragInfo<T> where T : class, IRecycleTable
{
    public bool restrictScrollView { set; get; }
    public Vector2 panelOffset { set; get; }
    public UIRecycleTable<T>.Direction dragDirection { set; get; }
    public bool instant { set; get; }
    public UIScrollView.Movement movement { set; get; }
    public bool isTopOrLeft
    {
        get
        {
            return (dragDirection & (UIRecycleTable<T>.Direction.Top | UIRecycleTable<T>.Direction.Left)) != 0;
        }
    }

    public bool isBottomOrRight
    {
        get
        {
            return (dragDirection & (UIRecycleTable<T>.Direction.Bottom | UIRecycleTable<T>.Direction.Right)) != 0;
        }
    }

    public float offset
    {
        get
        {
            switch (movement)
            {
                case UIScrollView.Movement.Horizontal:
                    return Mathf.Abs(panelOffset.x);
                case UIScrollView.Movement.Vertical:
                    return Mathf.Abs(panelOffset.y);
                default:
                    return 0F;
            }
        }
    }
}

/// <summary>
/// 扩展
/// </summary>
static class UIRecycleTableExtension
{
    static public void SetLocalPos(this Transform pTr, float pX, float pY)
    {
        pTr.localPosition = new Vector3(pX, pY, pTr.localPosition.z);
    }
    static public Vector3 ToVector3(this Vector2 pVec2)
    {
        return new Vector3(pVec2.x, pVec2.y, 0);
    }

    static public T Find<T>(HashSet<T> pSets, Predicate<T> pPredicate)
    {
        if (pSets == null || pSets.Count == 0 || pPredicate == null) return default(T);
        var tEnumerator = pSets.GetEnumerator();
        while (tEnumerator.MoveNext())
        {
            var tCurrent = tEnumerator.Current;
            if (pPredicate(tCurrent))
            {
                return tCurrent;
            }
        }
        return default(T);
    }

    static public T FindCustom<T, V>(HashSet<T> pSets, Func<T, V> pFunc, bool pFindMin) where V : IComparable<V>
    {
        if (pSets == null || pSets.Count == 0 || pFunc == null) return default(T);
        var tEnumerator = pSets.GetEnumerator();
        var tMinV = default(V);
        var tMinT = default(T);

        while (tEnumerator.MoveNext())
        {
            var tCurrentT = tEnumerator.Current;
            var tCurrentV = pFunc(tCurrentT);

            if (EqualityComparer<T>.Default.Equals(tMinT, default(T)))
            {
                tMinV = tCurrentV;
                tMinT = tCurrentT;
                continue;
            }

            var tVal = tCurrentV.CompareTo(tMinV);
            if (pFindMin)
            {
                if (tVal != -1) continue;
            }
            else
            {
                if (tVal != 1) continue;
            }
            tMinV = tCurrentV;
            tMinT = tCurrentT;
        }
        return tMinT;
    }
}