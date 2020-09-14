using System;
using System.Collections.Generic;
using System.Linq;
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
            t.ConfigureAwait(false);
            running_task_list.Add(t);
            if(running_task_list.Count>size)
            {
                await Task.WhenAll(running_task_list).ConfigureAwait(false);
                done_task_list.AddRange(running_task_list);
                running_task_list.Clear();
            }
        }
        public async Task Done()
        {
            if (running_task_list.Count > 0)
            {
                await Task.WhenAll(running_task_list).ConfigureAwait(false);
                done_task_list.AddRange(running_task_list);
                running_task_list.Clear();
            }
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
            t.ConfigureAwait(false);
            running_task_list.Add(t);
            if (running_task_list.Count > size)
            {
                await Task.WhenAll(running_task_list).ConfigureAwait(false);
                done_task_list.AddRange(running_task_list);
                running_task_list.Clear();
            }
        }
        public async Task Done()
        {
            if (running_task_list.Count > 0)
            {
                await Task.WhenAll(running_task_list).ConfigureAwait(false);
                done_task_list.AddRange(running_task_list);
                running_task_list.Clear();
            }
        }
    }
}
