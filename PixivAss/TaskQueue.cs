using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PixivAss
{
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
            if(running_task_list.Count>size)
            {
                await Task.WhenAll(running_task_list);
                done_task_list.AddRange(running_task_list);
                running_task_list.Clear();
            }
        }
        public async Task Done()
        {
            if (running_task_list.Count > 0)
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
    }
    class TaskQueue<T>
    {
        public List<Task<T>> running_task_list=new List<Task<T>>();
        public List<Task<T>> done_task_list = new List<Task<T>>();
        private int size = 0;
        public TaskQueue(int _size)
        {
            size = _size;
        }
        public async Task Add(Task<T> t)
        {
            running_task_list.Add(t);
            if (running_task_list.Count > size)
            {
                await Task.WhenAll(running_task_list);
                done_task_list.AddRange(running_task_list);
                running_task_list.Clear();
            }
        }
        public async Task Done()
        {
            if (running_task_list.Count > 0)
            {
                await Task.WhenAll(running_task_list);
                done_task_list.AddRange(running_task_list);
                running_task_list.Clear();
            }
        }
        public async Task<HashSet<int>> GetResultSet()
        {//暂时想不到更好的写法
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
                    foreach (var item in (List<int>)Convert.ChangeType(task.Result, typeof(List<string>)))
                        ret.Add(item);
                return ret;
            }
            else
                return null;
        }
    }
}
