using System;
using System.Collections.Generic;
using System.Timers;
using CommonUtils;
using CommonUtils.Helper;
using UnityEngine;

namespace Manager
{
    public enum TaskQueueType
    {
        Normal
    }

    public abstract class TasksBase
    {
        private readonly TaskQueueType _taskQueueType;
        public bool AutoClearCallbackFlag = true; //true 当回调执行完自动清除队列标记,flase 需要手动重置
        public bool ByCache = true;

        public TasksBase(TaskQueueType taskQueueType)
        {
            _taskQueueType = taskQueueType;
        }

        public void ClearCallbackFlag()
        {
            TasksCenterManager.Instance.SetIsCanQueue(true);
        }

        /// <summary>
        ///     ShowTaskWindow 方法,如果任务完成,调用该方法进行弹窗,然后在队列中准备执行回调
        ///     如果任务完成不需要弹窗,这个方法可以不用调用
        /// </summary>
        public virtual void ShowTaskWindow(int id)
        {
            TasksCenterManager.Instance.ShowTaskWindow(id, _taskQueueType, this);
        }

        /// <summary>
        ///     当触发任务完成,放入队列后出队执行的方法
        /// </summary>
        public abstract void TaskSuccessfulCallback(string id);

        /// <summary>
        ///     当触发任务刷新时执行的方法
        /// </summary>
        public abstract void Update();
    }

    public class TasksCenterManager : Singleton<TasksCenterManager>
    {
        private class TaskQueue
        {
            public string ID;
            public TasksBase TasksBase;
        }

        // 定时任务间隔（毫秒）
        private float _interval = 0.5f; // 当任务完成时每0.5秒执行一次队列
        private Timer _timer;
        private readonly Dictionary<string, bool> TaskShowDic = new(); //内存判断是否显示过,暂时无用
        private List<TasksBase> _taskList; //抽象任务,执行任务刷新方法
        private readonly Queue<TaskQueue> _taskQueue = new(); // 创建任务队列
        private bool _isCanQueue = true; //出队一次变为flase,如果回调中不置为true则下次不会出队
        private readonly string taskHasShowKeyPrefix = "TaskShow:";
        private readonly string taskShowLastTimeKeyPrefix = "TaskShowLastTime:";

        public void Initialize(List<TasksBase> taskList, float interval = 0.5f)
        {
            _taskList = taskList;
            _isCanQueue = true;
            _interval = interval;
            StartTimer();
        }

        public void SetIsCanQueue(bool flag)
        {
            _isCanQueue = flag;
        }

        public void ReSetTaskSuccessfulInterval(int interval)
        {
            _interval = interval;
            RestartTimer();
        }

        public void StartTimer()
        {
            // 初始化定时器，设置间隔时间（以毫秒为单位）
            _timer = new Timer(_interval);
            _timer.Elapsed += TimerElapsed;
            _timer.AutoReset = true;
            _timer.Start();
        }

        // 停止定时器
        public void StopTimer()
        {
            _timer.Stop();
            _timer.Dispose();
        }

        public void RestartTimer()
        {
            StopTimer();
            StartTimer();
        }

        // 定时器事件处理方法
        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_isCanQueue)
                if (_taskQueue.Count > 0)
                {
                    _isCanQueue = false;
                    var element = _taskQueue.Dequeue(); // 从队列中移除元素
                    element.TasksBase.TaskSuccessfulCallback(element.ID);
                    TaskShowDic[element.ID] = true;
                    if (element.TasksBase.AutoClearCallbackFlag) _isCanQueue = true;
                }
        }

        /// <summary>
        ///     Register ,注册任务,调用下面的 Update 即可统一刷新
        /// </summary>
        /// <param name="taskQueueType">参数 taskQueueType</param>
        public void Register(TasksBase tasksBase)
        {
            if (_taskList.Contains(tasksBase)) return;

            _taskList.Add(tasksBase);
        }

        /// <summary>
        ///     UpdateOnEvent 方法
        /// </summary>
        /// <param name="type">参数 type</param>
        public void Update()
        {
            foreach (var tasksBase in _taskList ?? new List<TasksBase>()) tasksBase.Update();
        }

        /// <summary>
        ///     ShowTaskWindow 方法
        /// </summary>
        public void ShowTaskWindow(int taskId, TaskQueueType type, TasksBase tasksBase)
        {
            //当次登录如果有提醒,则跳过
            var taskQueueId = taskId + "_" + (int)type;
            if (IsHasShowTaskWindow(taskQueueId, tasksBase.ByCache)) return;
            _taskQueue.Enqueue(new TaskQueue { ID = taskQueueId, TasksBase = tasksBase });
        }


        /// <summary>
        ///     检查任务是否已经展示过。首次进入提示 hasShow=false => true,lastTime=currentTime,方法返回 false , 显示提示框
        ///     当日再次进入 , lastTime 不变, hasShow=true 不执行逻辑, 方法返回 false , 显示提示框
        ///     隔天用户还是没领取任务进入,hasShow=true, lastTime和 currentTime 跨天使 hasShow=false,所以返回 false, 显示提示框
        /// </summary>
        /// <param name="byCache">ture: 使用内存,下次登录还会提示,false 持久化存储</param>
        private bool IsHasShowTaskWindow(string id, bool byCache = true)
        {
            if (byCache)
            {
                TaskShowDic.TryGetValue(id, out var result);
                return result;
            }

            var taskHasShowKey = taskHasShowKeyPrefix + id;
            var taskShowLastTimeKey = taskShowLastTimeKeyPrefix + id;
            var hasShow = PlayerPrefs.GetInt(taskHasShowKey) == 1;
            var lastTime = PlayerPrefs.GetString(taskShowLastTimeKey);
            long.TryParse(lastTime, out var lasTimeSeconds);
            var current = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            //如果跨天则重置,设置新的任务提示时间戳
            if (TimerHelper.GetCrossDays(lasTimeSeconds, current) >= 1)
            {
                PlayerPrefs.SetString(taskShowLastTimeKey, current.ToString());
                PlayerPrefs.SetInt(taskHasShowKey, 0);
                hasShow = false;
                PlayerPrefs.Save();
            }

            if (!hasShow)
            {
                PlayerPrefs.SetInt(taskHasShowKey, 1);
                PlayerPrefs.Save();
            }

            return hasShow;
        }

        public void RemoveSetting(List<string> ids)
        {
            foreach (var id in ids) RemoveSetting(id);
            TaskShowDic.Clear();
        }

        /// <summary>
        /// </summary>
        public void RemoveSetting(string id)
        {
            var taskHasShowKey = taskHasShowKeyPrefix + id;
            var taskShowLastTimeKey = taskShowLastTimeKeyPrefix + id;
            PlayerPrefs.DeleteKey(taskHasShowKey);
            PlayerPrefs.DeleteKey(taskShowLastTimeKey);
            TaskShowDic.Clear();
        }
    }
}