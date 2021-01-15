using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// ReSharper disable once CheckNamespace

namespace GameLovers
{
	/// <summary>
	/// A list with the possibility to observe changes to it's elements defined <see cref="ObservableUpdateType"/> rules
	/// </summary>
	public interface IObservableListReader : IEnumerable
	{
		/// <summary>
		/// Requests the list element count
		/// </summary>
		int Count { get; }
	}
	
	/// <inheritdoc cref="IObservableListReader"/>
	/// <remarks>
	/// Read only observable list interface
	/// </remarks>
	public interface IObservableListReader<T> :IObservableListReader, IEnumerable<T> where T : struct
	{
		/// <summary>
		/// Looks up and return the data that is associated with the given <paramref name="index"/>
		/// </summary>
		T this[int index] { get; }
		
		/// <summary>
		/// Requests this list as a <see cref="IReadOnlyList{T}"/>
		/// </summary>
		IReadOnlyList<T> ReadOnlyList { get; }

		/// <inheritdoc cref="List{T}.Contains"/>
		bool Contains(T value);

		/// <inheritdoc cref="List{T}.IndexOf(T)"/>
		int IndexOf(T value);
		
		/// <summary>
		/// Observes this list with the given <paramref name="onUpdate"/> when any data changes following the rule of
		/// the given <paramref name="updateType"/>
		/// </summary>
		void Observe(ObservableUpdateType updateType, Action<int, T> onUpdate);
		
		/// <summary>
		/// Observes this list with the given <paramref name="onUpdate"/> when any data changes following the rule of
		/// the given <paramref name="updateType"/> and invokes the given <paramref name="onUpdate"/> with the given <paramref name="index"/>
		/// </summary>
		void InvokeObserve(int index, ObservableUpdateType updateType, Action<int, T> onUpdate);
		
		/// <summary>
		/// Stops observing this list with the given <paramref name="onUpdate"/> of any data changes following the rule of
		/// the given <paramref name="updateType"/>
		/// </summary>
		void StopObserving(ObservableUpdateType updateType, Action<int, T> onUpdate);
	}

	/// <inheritdoc />
	public interface IObservableList<T> : IObservableListReader<T> where T : struct
	{
		/// <summary>
		/// Changes the given <paramref name="index"/> in the list. If the data does not exist it will be added.
		/// It will notify any observer listing to its data
		/// </summary>
		new T this[int index] { get; set; }
		
		/// <inheritdoc cref="List{T}.Remove"/>
		void Add(T data);
		
		/// <inheritdoc cref="List{T}.Remove"/>
		void Remove(T data);
		
		/// <inheritdoc cref="List{T}.RemoveAt"/>
		void RemoveAt(int index);
		
		/// <remarks>
		/// It invokes any update method that is observing to the given <paramref name="index"/> on this list
		/// </remarks>
		void InvokeUpdate(int index);
	}
	
	/// <inheritdoc />
	public class ObservableList<T> : IObservableList<T> where T : struct
	{
		private readonly IReadOnlyDictionary<int, IList<Action<int, T>>> _genericUpdateActions = 
			new ReadOnlyDictionary<int, IList<Action<int, T>>>(new Dictionary<int, IList<Action<int, T>>>
			{
				{(int) ObservableUpdateType.Added, new List<Action<int, T>>()},
				{(int) ObservableUpdateType.Removed, new List<Action<int, T>>()},
				{(int) ObservableUpdateType.Updated, new List<Action<int, T>>()}
			});

		/// <inheritdoc cref="IObservableList{T}.this" />
		public T this[int index]
		{
			get => List[index];
			set
			{
				List[index] = value;
				
				InvokeUpdate(index);
			}
		}
		
		/// <inheritdoc />
		public int Count => List.Count;
		/// <inheritdoc />
		public IReadOnlyList<T> ReadOnlyList => List;
		
		protected virtual List<T> List { get; }
		
		protected ObservableList() {}
		
		public ObservableList(List<T> list)
		{
			List = list;
		}

		/// <inheritdoc cref="List{T}.GetEnumerator"/>
		public List<T>.Enumerator GetEnumerator()
		{
			return List.GetEnumerator();
		}

		/// <inheritdoc />
		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return List.GetEnumerator();
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator()
		{
			return List.GetEnumerator();
		}

		/// <inheritdoc />
		public bool Contains(T value)
		{
			return List.Contains(value);
		}

		/// <inheritdoc />
		public int IndexOf(T value)
		{
			return List.IndexOf(value);
		}

		/// <inheritdoc />
		public void Add(T data)
		{
			List.Add(data);

			var updates = _genericUpdateActions[(int) ObservableUpdateType.Added];
			for (var i = 0; i < updates.Count; i++)
			{
				updates[i](i, data);
			}
		}

		/// <inheritdoc />
		public void Remove(T data)
		{
			List.Remove(data);

			var updates = _genericUpdateActions[(int) ObservableUpdateType.Removed];
			for (var i = 0; i < updates.Count; i++)
			{
				updates[i](i, data);
			}
		}

		/// <inheritdoc />
		public void RemoveAt(int index)
		{
			var data = List[index];
			
			List.RemoveAt(index);

			var updates = _genericUpdateActions[(int) ObservableUpdateType.Removed];
			for (var i = 0; i < updates.Count; i++)
			{
				updates[i](i, data);
			}
		}

		/// <inheritdoc />
		public void Observe(ObservableUpdateType updateType, Action<int, T> onUpdate)
		{
			_genericUpdateActions[(int) updateType].Add(onUpdate);
		}

		/// <inheritdoc />
		public void InvokeObserve(int index, ObservableUpdateType updateType, Action<int, T> onUpdate)
		{
			onUpdate(index, List[index]);
			
			Observe(updateType, onUpdate);
		}

		/// <inheritdoc />
		public void InvokeUpdate(int index)
		{
			var value = List[index];
			
			var updates = _genericUpdateActions[(int) ObservableUpdateType.Updated];
			for (var i = 0; i < updates.Count; i++)
			{
				updates[i](i, value);
			}
		}

		/// <inheritdoc />
		public void StopObserving(ObservableUpdateType updateType, Action<int, T> onUpdate)
		{
			_genericUpdateActions[(int) updateType].Remove(onUpdate);
		}
	}

	/// <inheritdoc />
	public class ObservableResolverList<T> : ObservableList<T> where T : struct
	{
		private readonly Func<List<T>> _listResolver;

		protected override List<T> List => _listResolver();

		public ObservableResolverList(Func<List<T>> listResolver)
		{
			_listResolver = listResolver;
		}
	}
}