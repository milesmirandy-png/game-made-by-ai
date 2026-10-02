using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Minimal reusable pool for objects that appear and disappear often.
    public class ObjectPool<T> where T : Component
    {
        readonly Stack<T> free = new Stack<T>();
        readonly System.Func<T> factory;

        public ObjectPool(System.Func<T> create)
        {
            factory = create;
        }

        public T Get()
        {
            while (free.Count > 0)
            {
                var item = free.Pop();
                if (item == null) continue;
                item.gameObject.SetActive(true);
                return item;
            }
            var created = factory();
            created.gameObject.SetActive(true);
            return created;
        }

        public void Release(T item)
        {
            if (item == null) return;
            item.gameObject.SetActive(false);
            free.Push(item);
        }
    }
}
