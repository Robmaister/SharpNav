using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpNav.Collections.Generic
{
    /// <summary>
    /// A single node of a linked list.
    /// </summary>
    public class ListNode
    {
        public ListNode next;
        public Object data;


        /// <summary>
        /// Initializes a new ListNode object with no data or next pointer.
        /// </summary>
        public ListNode()
        {
            data = null; 
            next = null;
        }


        /// <summary>
        /// Initializes ListNode object to pre-defined data value.
        /// </summary>
        /// <param name="_data"></param>
        public ListNode(Object _data) 
        {
            data = _data;
            next = null;
        }
    }


    /// <summary>
    /// The primary linked list class. Contains a pointer to the head element along with
    /// the number of elements in the list, and functions to operate on the list. 
    /// </summary>
    public class LinkedList
    {
        private ListNode head;
        private int size;

        /// <summary>
        /// Creates a new empty (default) linked list.
        /// </summary>
        public LinkedList()
        {
            head = null;
            size = 0;
        }


        /// <summary>
        /// Pre-initializes a new LinkedList object to another head pointer
        /// </summary>
        /// <param name="_head"></param>
        public LinkedList(ListNode _head)
        {
            head = _head;
            size = 0;

            ListNode current = head;
            while (current != null)
            {
                size++;
                current = current.next;
            }
        }


        /// <summary>
        /// Adds new element to the back of the linked list.
        /// </summary>
        /// <param name="element">Element to be added</param>
        public void PushBack(Object element)
        {
            ListNode current = head;
            while (current.next != null)
                current = current.next;
            current.next = new ListNode(element);
            size++; 
        }


        /// <summary>
        /// Adds a new element to the front of the linked list.
        /// </summary>
        /// <param name="element">Element to be added</param>
        public void PushFront(Object element)
        {
            if (head == null)
            {
                head = new ListNode(element);
                size = 1;
                return;
            }

            ListNode newHead = new ListNode(element);
            newHead.next = head;
            head = newHead;
            size++;
        }
    }
}
