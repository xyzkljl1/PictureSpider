using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider
{
    /*
    class TaskQueue
    {
        private List<Task> running_task_list = new List<Task>();
        private List<Task> done_task_list = new List<Task>();
        private int size = 0;
        public TaskQueue(int _size)
        {
            size = _size;
        }
        public async Task Add(Task t)
        {
            running_task_list.Add(t);
            await Wait(size);
        }
        public async Task Done()
        {
            await Wait(0);
        }
        private async Task Wait(int target)//当队列大于target时清空
        {
            if (running_task_list.Count > target)
            {
                await Task.WhenAll(running_task_list);
                done_task_list.AddRange(running_task_list);
                running_task_list.Clear();
            }
        }

        public static async Task<HashSet<string>> GetResultSet(TaskQueue<List<string>> queue)
        {
            await queue.Done();
            var ret = new HashSet<string>();
            foreach (var task in queue.done_task_list)
                foreach (var item in task.Result)
                    ret.Add(item);
            return ret;
        }
        public static async Task<HashSet<string>> GetResultSet(TaskQueue<string> queue)
        {
            await queue.Done();
            var ret = new HashSet<string>();
            foreach (var task in queue.done_task_list)
                ret.Add(task.Result);
            return ret;
        }
    }*/
    /*
     * queue本身并不会令任务并行
     * IO任务应当如queue.Add(func());般调用
     * CPU任务应当如queue.Add(Task.Run(()=>func()));般调用,注意线程安全
     */
    class TaskQueue<T>
    {
        public List<Task<T>> running_task_list=new List<Task<T>>();
        public List<Task<T>> done_task_list = new List<Task<T>>();
        private Action<List<Task<T>>> processor=null;
        private int queue_size = 0;//并发限制，即running_task_list大小限制
        private int storage_size = 0;//存储限制，即done_task_list大小限制，仅当processor不为空时有效
        public TaskQueue(int _size,int _psize=0, Action<List<Task<T>>> _processor =null)
        {
            queue_size = _size;
            storage_size = _psize;
            processor = _processor;
        }
        public async Task Add(Task<T> t)
        {
            if (t is null) // null会导致whenany抛异常
                return;
            running_task_list.Add(t);
            await WaitUntil(queue_size,storage_size);
        }
        public async Task Done()
        {
            await WaitUntil(0,0);
        }
        private async Task WaitUntil(int queue_target,int storage_size)//等待直到队列小于指定size
        {
            //由于外部已经限制了速率，队列改为全速
            while(running_task_list.Count>queue_target)
            {
                var task=await Task.WhenAny(running_task_list);
                done_task_list.Add(task);
                running_task_list.Remove(task);
                if (!(processor is null))
                    if (done_task_list.Count > storage_size)
                    {
                        processor(done_task_list);
                        done_task_list.Clear();
                    }
            }
        }
        public async Task<HashSet<int>> GetResultSet()
        {//暂时想不到更好的写法
            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                if (typeof(T) == typeof(int))
                {
                    await Done();
                    var ret = new HashSet<int>();
                    foreach (var task in done_task_list)
                        ret.Add((int)Convert.ChangeType(task.Result, typeof(int)));
                    return ret;
                }
                else if (typeof(T) == typeof(List<int>))
                {
                    await Done();
                    var ret = new HashSet<int>();
                    foreach (var task in done_task_list)
                        foreach (var item in (List<int>)Convert.ChangeType(task.Result, typeof(List<int>)))
                            ret.Add((int)Convert.ChangeType(item, typeof(int)));
                    return ret;
                }
                else
                    return null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }
    }
}
