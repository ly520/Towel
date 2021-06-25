﻿using BenchmarkDotNet.Attributes;
using System;
using System.Runtime.CompilerServices;
using Towel;
using Towel.DataStructures;
using static Towel.Statics;

namespace Towel_Benchmarking
{
	[Tag(Program.Name, "Heap Generics Vs Delegates")]
	[Tag(Program.OutputFile, nameof(HeapGenericsVsDelegatesBenchmarks))]
	public class HeapGenericsVsDelegatesBenchmarks
	{
		[ParamsSource(nameof(RandomData))]
		public Person[]? RandomTestData { get; set; }

		public static Person[][] RandomData => Towel_Benchmarking.RandomData.DataStructures.RandomData;

		[Benchmark]
		public void HeapGAD_Add()
		{
			var heap = new HeapArrayAggressiveDefault<Person, PersonDobCompare>();
			foreach (Person person in RandomTestData!)
			{
				heap.Enqueue(person);
			}
		}

		[Benchmark]
		public void HeapGA_Add()
		{
			var heap = new HeapArrayAggressive<Person, PersonDobCompare>();
			foreach (Person person in RandomTestData!)
			{
				heap.Enqueue(person);
			}
		}

		[Benchmark]
		public void HeapG_Add()
		{
			var heap = new HeapArray<Person, PersonDobCompare>();
			foreach (Person person in RandomTestData!)
			{
				heap.Enqueue(person);
			}
		}

		[Benchmark]
		public void HeapD_Enqueue()
		{
			var heap = new HeapArray<Person>((a, b) => Statics.ToCompareResult(a.DateOfBirth.CompareTo(b.DateOfBirth)));
			foreach (Person person in RandomTestData!)
			{
				heap.Enqueue(person);
			}
		}
	}

	public struct PersonDobCompare : IFunc<Person, Person, CompareResult> { public CompareResult Invoke(Person a, Person b) => ToCompareResult(a.DateOfBirth.CompareTo(b.DateOfBirth)); }

	#region HeapArray Delegate

	public class HeapArray<T> : IHeap<T>,
		ICloneable<HeapArray<T>>
	{
		internal const int _root = 1; // The root index of the heap.

		internal Func<T, T, CompareResult> _compare;
		internal T[] _array;
		internal int? _minimumCapacity;
		internal int _count;

		#region Constructors

		/// <summary>
		/// Generates a priority queue with a capacity of the parameter.
		/// <para>Runtime: O(1)</para>
		/// </summary>
		/// <param name="compare">Delegate determining the comparison technique used for sorting.</param>
		/// <param name="minimumCapacity">The capacity you want this priority queue to have.</param>
		public HeapArray(Func<T, T, CompareResult>? compare = default, int? minimumCapacity = null)
		{
			if (minimumCapacity is not null && minimumCapacity.Value < 1) throw new ArgumentOutOfRangeException(message: "value.Value < 1", paramName: nameof(minimumCapacity));
			compare ??= Statics.Compare;
			_compare = compare;
			_minimumCapacity = minimumCapacity;
			_array = new T[(_minimumCapacity ?? 0) + _root];
			_count = 0;
		}

		internal HeapArray(HeapArray<T> heap)
		{
			_compare = heap._compare;
			_minimumCapacity = heap._minimumCapacity;
			_array = (T[])heap._array.Clone();
			_count = heap._count;
		}

		#endregion

		#region Properties

		/// <inheritdoc/>
		public Func<T, T, CompareResult> Compare => _compare;

		/// <summary>
		/// The maximum items the queue can hold.
		/// <para>Runtime: O(1)</para>
		/// </summary>
		public int CurrentCapacity => _array.Length - 1;

		/// <summary>
		/// The minumum capacity of this queue to limit low-level resizing.
		/// <para>Runtime: O(1)</para>
		/// </summary>
		public int? MinimumCapacity
		{
			get => _minimumCapacity;
			set
			{
				if (value is not null)
				{
					if (value.Value < 1) throw new ArgumentOutOfRangeException(message: "value.Value < 1", paramName: nameof(value));
					else if (value.Value > _array.Length + 1) Array.Resize<T>(ref _array, value.Value + 1);
				}
				_minimumCapacity = value;
			}
		}

		/// <inheritdoc/>
		public int Count => _count;

		#endregion

		#region Methods

		/// <summary>Gets the index of the left child of the provided item.</summary>
		/// <param name="parent">The item to find the left child of.</param>
		/// <returns>The index of the left child of the provided item.</returns>
		internal static int LeftChild(int parent) => parent * 2;

		/// <summary>Gets the index of the right child of the provided item.</summary>
		/// <param name="parent">The item to find the right child of.</param>
		/// <returns>The index of the right child of the provided item.</returns>
		internal static int RightChild(int parent) => parent * 2 + 1;

		/// <summary>Gets the index of the parent of the provided item.</summary>
		/// <param name="child">The item to find the parent of.</param>
		/// <returns>The index of the parent of the provided item.</returns>
		internal static int Parent(int child) => child / 2;

		/// <inheritdoc/>
		public void Enqueue(T value)
		{
			if (_count + 1 >= _array.Length)
			{
				if (_array.Length is int.MaxValue)
				{
					throw new InvalidOperationException("this heap has become too large");
				}
				Array.Resize<T>(ref _array, _array.Length > int.MaxValue / 2 ? int.MaxValue : _array.Length * 2);
			}
			_count++;
			_array[_count] = value;
			ShiftUp(_count);
		}

		/// <inheritdoc/>
		public T Dequeue()
		{
			if (_count > 0)
			{
				T removal = _array[_root];
				Swap(ref _array[_root], ref _array[_count]);
				_count--;
				ShiftDown(_root);
				return removal;
			}
			throw new InvalidOperationException("Attempting to remove from an empty priority queue.");
		}

		/// <summary>
		/// Requeues an item after a change has occured.
		/// <para>Runtime: O(n)</para>
		/// </summary>
		/// <param name="item">The item to requeue.</param>
		public void Requeue(T item)
		{
			int i;
			for (i = 1; i <= _count; i++)
			{
				if (_compare.Invoke(item, _array[i]) is Equal)
				{
					break;
				}
			}
			if (i > _count)
			{
				throw new InvalidOperationException("Attempting to re-queue an item that is not in the heap.");
			}
			ShiftUp(i);
			ShiftDown(i);
		}

		/// <inheritdoc/>
		public T Peek()
		{
			if (_count > 0)
			{
				return _array[_root];
			}
			throw new InvalidOperationException("Attempting to peek at an empty priority queue.");
		}

		/// <summary>
		/// Standard priority queue algorithm for up sifting.
		/// <para>Runtime: O(ln(n)), Ω(1)</para>
		/// </summary>
		/// <param name="index">The index to be up sifted.</param>
		internal void ShiftUp(int index)
		{
			int parent;
			while ((parent = Parent(index)) > 0 && _compare.Invoke(_array[index], _array[parent]) is Greater)
			{
				Swap(ref _array[index], ref _array[parent]);
				index = parent;
			}
		}

		/// <summary>
		/// Standard priority queue algorithm for sifting down.
		/// <para>Runtime: O(ln(n)), Ω(1)</para>
		/// </summary>
		/// <param name="index">The index to be down sifted.</param>
		internal void ShiftDown(int index)
		{
			int leftChild, rightChild;
			while ((leftChild = LeftChild(index)) <= _count)
			{
				int down = leftChild;
				if ((rightChild = RightChild(index)) <= _count && _compare.Invoke(_array[rightChild], _array[leftChild]) is Greater)
				{
					down = rightChild;
				}
				if (_compare.Invoke(_array[down], _array[index]) is Less)
				{
					break;
				}
				Swap(ref _array[index], ref _array[down]);
				index = down;
			}
		}

		/// <inheritdoc/>
		public void Clear() => _count = 0;

		/// <inheritdoc/>
		public T[] ToArray() => _array[1..(_count + 1)];

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

		/// <inheritdoc/>
		public System.Collections.Generic.IEnumerator<T> GetEnumerator()
		{
			for (int i = 1; i <= _count; i++)
			{
				yield return _array[i];
			}
		}

		/// <inheritdoc/>
		public StepStatus StepperBreak<TStep>(TStep step = default)
			where TStep : struct, IFunc<T, StepStatus> =>
			_array.StepperBreak(1, _count + 1, step);

		/// <inheritdoc/>
		public HeapArray<T> Clone() => new(this);

		#endregion
	}

	#endregion

	#region Heap Array Aggrssive

	/// <summary>A heap with static priorities implemented as a array.</summary>
	/// <typeparam name="T">The type of values stored in this data structure.</typeparam>
	/// <typeparam name="TCompare">The type that is comparing <typeparamref name="T"/> values.</typeparam>
	public class HeapArrayAggressive<T, TCompare> : IHeap<T, TCompare>, IHeap<T>,
		ICloneable<HeapArrayAggressive<T, TCompare>>
		where TCompare : struct, IFunc<T, T, CompareResult>
	{
		internal const int _root = 1; // The root index of the heap.

		internal TCompare _compare;
		internal T[] _array;
		internal int? _minimumCapacity;
		internal int _count;

		#region Constructors

		/// <summary>
		/// Generates a priority queue with a capacity of the parameter.
		/// <para>Runtime: O(1)</para>
		/// </summary>
		/// <param name="compare">Delegate determining the comparison technique used for sorting.</param>
		/// <param name="minimumCapacity">The capacity you want this priority queue to have.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public HeapArrayAggressive(TCompare compare = default, int? minimumCapacity = null)
		{
			if (minimumCapacity is not null && minimumCapacity.Value < 1) throw new ArgumentOutOfRangeException(message: "value.Value < 1", paramName: nameof(minimumCapacity));
			_compare = compare;
			_minimumCapacity = minimumCapacity;
			_array = new T[(_minimumCapacity ?? 0) + _root];
			_count = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal HeapArrayAggressive(HeapArrayAggressive<T, TCompare> heap)
		{
			_compare = heap._compare;
			_minimumCapacity = heap._minimumCapacity;
			_array = (T[])heap._array.Clone();
			_count = heap._count;
		}

		#endregion

		#region Properties

		/// <inheritdoc/>
		public TCompare Compare => _compare;

		/// <summary>
		/// The maximum items the queue can hold.
		/// <para>Runtime: O(1)</para>
		/// </summary>
		public int CurrentCapacity => _array.Length - 1;

		/// <summary>
		/// The minumum capacity of this queue to limit low-level resizing.
		/// <para>Runtime: O(1)</para>
		/// </summary>
		public int? MinimumCapacity
		{
			get => _minimumCapacity;
			set
			{
				if (value is not null)
				{
					if (value.Value < 1) throw new ArgumentOutOfRangeException(message: "value.Value < 1", paramName: nameof(value));
					else if (value.Value > _array.Length + 1) Array.Resize<T>(ref _array, value.Value + 1);
				}
				_minimumCapacity = value;
			}
		}

		/// <inheritdoc/>
		public int Count => _count;

		#endregion

		#region Methods

		/// <summary>Gets the index of the left child of the provided item.</summary>
		/// <param name="parent">The item to find the left child of.</param>
		/// <returns>The index of the left child of the provided item.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal static int LeftChild(int parent) => parent * 2;

		/// <summary>Gets the index of the right child of the provided item.</summary>
		/// <param name="parent">The item to find the right child of.</param>
		/// <returns>The index of the right child of the provided item.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal static int RightChild(int parent) => parent * 2 + 1;

		/// <summary>Gets the index of the parent of the provided item.</summary>
		/// <param name="child">The item to find the parent of.</param>
		/// <returns>The index of the parent of the provided item.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal static int Parent(int child) => child / 2;

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void Enqueue(T value)
		{
			if (_count + 1 >= _array.Length)
			{
				if (_array.Length is int.MaxValue)
				{
					throw new InvalidOperationException("this heap has become too large");
				}
				Array.Resize<T>(ref _array, _array.Length > int.MaxValue / 2 ? int.MaxValue : _array.Length * 2);
			}
			_count++;
			_array[_count] = value;
			ShiftUp(_count);
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public T Dequeue()
		{
			if (_count > 0)
			{
				T removal = _array[_root];
				Swap(ref _array[_root], ref _array[_count]);
				_count--;
				ShiftDown(_root);
				return removal;
			}
			throw new InvalidOperationException("Attempting to remove from an empty priority queue.");
		}

		/// <summary>
		/// Requeues an item after a change has occured.
		/// <para>Runtime: O(n)</para>
		/// </summary>
		/// <param name="item">The item to requeue.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void Requeue(T item)
		{
			int i;
			for (i = 1; i <= _count; i++)
			{
				if (_compare.Invoke(item, _array[i]) is Equal)
				{
					break;
				}
			}
			if (i > _count)
			{
				throw new InvalidOperationException("Attempting to re-queue an item that is not in the heap.");
			}
			ShiftUp(i);
			ShiftDown(i);
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public T Peek()
		{
			if (_count > 0)
			{
				return _array[_root];
			}
			throw new InvalidOperationException("Attempting to peek at an empty priority queue.");
		}

		/// <summary>
		/// Standard priority queue algorithm for up sifting.
		/// <para>Runtime: O(ln(n)), Ω(1)</para>
		/// </summary>
		/// <param name="index">The index to be up sifted.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal void ShiftUp(int index)
		{
			int parent;
			while ((parent = Parent(index)) > 0 && _compare.Invoke(_array[index], _array[parent]) is Greater)
			{
				Swap(ref _array[index], ref _array[parent]);
				index = parent;
			}
		}

		/// <summary>
		/// Standard priority queue algorithm for sifting down.
		/// <para>Runtime: O(ln(n)), Ω(1)</para>
		/// </summary>
		/// <param name="index">The index to be down sifted.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal void ShiftDown(int index)
		{
			int leftChild, rightChild;
			while ((leftChild = LeftChild(index)) <= _count)
			{
				int down = leftChild;
				if ((rightChild = RightChild(index)) <= _count && _compare.Invoke(_array[rightChild], _array[leftChild]) is Greater)
				{
					down = rightChild;
				}
				if (_compare.Invoke(_array[down], _array[index]) is Less)
				{
					break;
				}
				Swap(ref _array[index], ref _array[down]);
				index = down;
			}
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void Clear() => _count = 0;

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public T[] ToArray() => _array[1..(_count + 1)];


		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public System.Collections.Generic.IEnumerator<T> GetEnumerator()
		{
			for (int i = 1; i <= _count; i++)
			{
				yield return _array[i];
			}
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public StepStatus StepperBreak<TStep>(TStep step = default)
			where TStep : struct, IFunc<T, StepStatus> =>
			_array.StepperBreak(1, _count + 1, step);

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public HeapArrayAggressive<T, TCompare> Clone() => new(this);

		#endregion
	}

	#endregion

	#region Heap Array Aggrssive

	/// <summary>A heap with static priorities implemented as a array.</summary>
	/// <typeparam name="T">The type of values stored in this data structure.</typeparam>
	/// <typeparam name="TCompare">The type that is comparing <typeparamref name="T"/> values.</typeparam>
	public class HeapArrayAggressiveDefault<T, TCompare> : IHeap<T, TCompare>, IHeap<T>,
		ICloneable<HeapArrayAggressiveDefault<T, TCompare>>
		where TCompare : struct, IFunc<T, T, CompareResult>
	{
		internal const int _root = 1; // The root index of the heap.

		internal T[] _array;
		internal int? _minimumCapacity;
		internal int _count;

		#region Constructors

		/// <summary>
		/// Generates a priority queue with a capacity of the parameter.
		/// <para>Runtime: O(1)</para>
		/// </summary>
		/// <param name="compare">Delegate determining the comparison technique used for sorting.</param>
		/// <param name="minimumCapacity">The capacity you want this priority queue to have.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public HeapArrayAggressiveDefault(int? minimumCapacity = null)
		{
			if (minimumCapacity is not null && minimumCapacity.Value < 1) throw new ArgumentOutOfRangeException(message: "value.Value < 1", paramName: nameof(minimumCapacity));
			_minimumCapacity = minimumCapacity;
			_array = new T[(_minimumCapacity ?? 0) + _root];
			_count = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal HeapArrayAggressiveDefault(HeapArrayAggressiveDefault<T, TCompare> heap)
		{
			_minimumCapacity = heap._minimumCapacity;
			_array = (T[])heap._array.Clone();
			_count = heap._count;
		}

		#endregion

		#region Properties

		/// <inheritdoc/>
		public TCompare Compare => default;

		/// <summary>
		/// The maximum items the queue can hold.
		/// <para>Runtime: O(1)</para>
		/// </summary>
		public int CurrentCapacity => _array.Length - 1;

		/// <summary>
		/// The minumum capacity of this queue to limit low-level resizing.
		/// <para>Runtime: O(1)</para>
		/// </summary>
		public int? MinimumCapacity
		{
			get => _minimumCapacity;
			set
			{
				if (value is not null)
				{
					if (value.Value < 1) throw new ArgumentOutOfRangeException(message: "value.Value < 1", paramName: nameof(value));
					else if (value.Value > _array.Length + 1) Array.Resize<T>(ref _array, value.Value + 1);
				}
				_minimumCapacity = value;
			}
		}

		/// <inheritdoc/>
		public int Count => _count;

		#endregion

		#region Methods

		/// <summary>Gets the index of the left child of the provided item.</summary>
		/// <param name="parent">The item to find the left child of.</param>
		/// <returns>The index of the left child of the provided item.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal static int LeftChild(int parent) => parent * 2;

		/// <summary>Gets the index of the right child of the provided item.</summary>
		/// <param name="parent">The item to find the right child of.</param>
		/// <returns>The index of the right child of the provided item.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal static int RightChild(int parent) => parent * 2 + 1;

		/// <summary>Gets the index of the parent of the provided item.</summary>
		/// <param name="child">The item to find the parent of.</param>
		/// <returns>The index of the parent of the provided item.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal static int Parent(int child) => child / 2;

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void Enqueue(T value)
		{
			if (_count + 1 >= _array.Length)
			{
				if (_array.Length is int.MaxValue)
				{
					throw new InvalidOperationException("this heap has become too large");
				}
				Array.Resize<T>(ref _array, _array.Length > int.MaxValue / 2 ? int.MaxValue : _array.Length * 2);
			}
			_count++;
			_array[_count] = value;
			ShiftUp(_count);
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public T Dequeue()
		{
			if (_count > 0)
			{
				T removal = _array[_root];
				Swap(ref _array[_root], ref _array[_count]);
				_count--;
				ShiftDown(_root);
				return removal;
			}
			throw new InvalidOperationException("Attempting to remove from an empty priority queue.");
		}

		/// <summary>
		/// Requeues an item after a change has occured.
		/// <para>Runtime: O(n)</para>
		/// </summary>
		/// <param name="item">The item to requeue.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void Requeue(T item)
		{
			int i;
			for (i = 1; i <= _count; i++)
			{
				if (default(TCompare).Invoke(item, _array[i]) is Equal)
				{
					break;
				}
			}
			if (i > _count)
			{
				throw new InvalidOperationException("Attempting to re-queue an item that is not in the heap.");
			}
			ShiftUp(i);
			ShiftDown(i);
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public T Peek()
		{
			if (_count > 0)
			{
				return _array[_root];
			}
			throw new InvalidOperationException("Attempting to peek at an empty priority queue.");
		}

		/// <summary>
		/// Standard priority queue algorithm for up sifting.
		/// <para>Runtime: O(ln(n)), Ω(1)</para>
		/// </summary>
		/// <param name="index">The index to be up sifted.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal void ShiftUp(int index)
		{
			int parent;
			while ((parent = Parent(index)) > 0 && default(TCompare).Invoke(_array[index], _array[parent]) is Greater)
			{
				Swap(ref _array[index], ref _array[parent]);
				index = parent;
			}
		}

		/// <summary>
		/// Standard priority queue algorithm for sifting down.
		/// <para>Runtime: O(ln(n)), Ω(1)</para>
		/// </summary>
		/// <param name="index">The index to be down sifted.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		internal void ShiftDown(int index)
		{
			int leftChild, rightChild;
			while ((leftChild = LeftChild(index)) <= _count)
			{
				int down = leftChild;
				if ((rightChild = RightChild(index)) <= _count && default(TCompare).Invoke(_array[rightChild], _array[leftChild]) is Greater)
				{
					down = rightChild;
				}
				if (default(TCompare).Invoke(_array[down], _array[index]) is Less)
				{
					break;
				}
				Swap(ref _array[index], ref _array[down]);
				index = down;
			}
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void Clear() => _count = 0;

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public T[] ToArray() => _array[1..(_count + 1)];


		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public System.Collections.Generic.IEnumerator<T> GetEnumerator()
		{
			for (int i = 1; i <= _count; i++)
			{
				yield return _array[i];
			}
		}

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public StepStatus StepperBreak<TStep>(TStep step = default)
			where TStep : struct, IFunc<T, StepStatus> =>
			_array.StepperBreak(1, _count + 1, step);

		/// <inheritdoc/>
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public HeapArrayAggressiveDefault<T, TCompare> Clone() => new(this);

		#endregion
	}

	#endregion
}
