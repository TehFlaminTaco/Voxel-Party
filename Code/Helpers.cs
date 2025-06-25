using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.UI;

public static class Helpers
{
	/// <summary>
	/// Divides two integers and rounds down to the lowest integer.
	/// </summary>
	/// <param name="a"></param>
	/// <param name="b"></param>
	/// <returns></returns>
	public static int FloorDiv( this int a, int b )
	{
		return (int)System.Math.Floor( (double)a / b );
	}

	public static Vector3Int Floor( this Vector3 vector )
	{
		return new Vector3Int(
			(int)System.Math.Floor( vector.x ),
			(int)System.Math.Floor( vector.y ),
			(int)System.Math.Floor( vector.z )
		);
	}

	public static Vector3Int Ceil( this Vector3 vector )
	{
		return new Vector3Int(
			(int)System.Math.Ceiling( vector.x ),
			(int)System.Math.Ceiling( vector.y ),
			(int)System.Math.Ceiling( vector.z )
		);
	}

	// Modulo and Remainder methods for Vector3
	// a modulo b is defined as (a % b + b) % b to ensure the result is always positive.
	// Where as remainder is simply a % b, which can be negative if a is negative.
	public static Vector3 Modulo( this Vector3 vector, Vector3 mod )
	{
		return new Vector3(
			((vector.x % mod.x) + mod.x) % mod.x,
			((vector.y % mod.y) + mod.y) % mod.y,
			((vector.z % mod.z) + mod.z) % mod.z
		);
	}

	public static Vector3 Remainder( this Vector3 vector, Vector3 mod )
	{
		return new Vector3(
			vector.x % mod.x,
			vector.y % mod.y,
			vector.z % mod.z
		);
	}

	public static Vector3 Fractional( this Vector3 vector )
	{
		return vector.Modulo( Vector3.One );
	}

	public static Vector3Int Modulo( this Vector3Int vector, Vector3Int mod )
	{
		return new Vector3Int(
			((vector.x % mod.x) + mod.x) % mod.x,
			((vector.y % mod.y) + mod.y) % mod.y,
			((vector.z % mod.z) + mod.z) % mod.z
		);
	}

	public static Vector3Int Remainder( this Vector3Int vector, Vector3Int mod )
	{
		return new Vector3Int(
			vector.x % mod.x,
			vector.y % mod.y,
			vector.z % mod.z
		);
	}

	public static BBox Scale( this BBox box, Vector3 amounts )
	{
		return new BBox( box.Mins * amounts, box.Maxs * amounts );
	}

	public static IEnumerable<T> TakeRandom<T>( this Random rng, IEnumerable<T> source, int count )
	{
		if ( count > source.Count() )
		{
			count = source.Count();
		}
		if ( count <= 0 )
		{
			yield break; // No items to take
		}

		// We're going to be annoying and take from it multiple times, so it's fastest to convert to a list first.
		// And iterate only once.
		var l = source.ToList();
		var taken = new HashSet<int>();
		for ( int i = 0; i < count; i++ )
		{
			int index = rng.Next( l.Count - taken.Count );
			// This takes a random index from the list, but gives 'padding' for already taken indices.
			// In order for this to work, we then need to add 1 to the index for every taken index that is less than the current index.
			// EG. Assume there are 10 elements (0 - 9), and the element '5' was already taken
			// We roll a number between 0 and 8 inclusive
			// If that number is 5,6,7 or 8, we need to add 1 to the index, because 5 is already taken.
			// Which makes the effective range of indicies 0-4 GAP 6-9
			foreach ( var t in taken )
			{
				if ( t <= index )
				{
					index++;
				}
			}
			// At this point, taken can never contain the index thanks to the above math. (There might be a better way to do this?)
			taken.Add( index );
			yield return l[index];
		}
	}

	public static IEnumerable<byte> RunLengthEncodeBy( this IEnumerable<byte> data, int stride )
	{
		if ( stride <= 0 )
		{
			throw new ArgumentException( "Stride must be greater than 0", nameof( stride ) );
		}
		var chunks = data.Chunk( stride );
		List<(int count, List<byte> chunk)> GroupedChunks = new List<(int count, List<byte> chunk)>();
		foreach ( var chunk in chunks )
		{
			// Non-repeat spans are in chunks of 0-63, which represent sizes of 1-64.
			// Which leaves the values of 64 - 255 to represent repeat spans. (255 - 63) + 1 gives 193 unique ways to represent repeat spans. We use 194, as the counts of 0 and 1 are illogical
			if ( GroupedChunks.Count == 0 || !GroupedChunks.Last().chunk.SequenceEqual( chunk ) || GroupedChunks.Last().count >= 193 )
			{
				GroupedChunks.Add( (1, new List<byte>( chunk )) );
			}
			else
			{
				var last = GroupedChunks.Last();
				last.count++;
				GroupedChunks[GroupedChunks.Count - 1] = last;
			}
		}

		List<byte> encoded = new List<byte>();
		for ( int i = 0; i < GroupedChunks.Count; i++ )
		{
			// If group count is 1, find as many count 1 groups immediately after them (Up to 64), encode that count, and then encode them in sequence
			if ( GroupedChunks[i].count == 1 )
			{
				int count = 1;
				int scanIndex = i + 1;
				while ( scanIndex < GroupedChunks.Count && GroupedChunks[scanIndex].count == 1 && count < 64 )
				{
					count++;
					scanIndex++;
				}
				encoded.Add( (byte)(count - 1) ); // Count is 0-63, so we subtract 1
				for ( int j = i; j < scanIndex; j++ )
				{
					encoded.AddRange( GroupedChunks[j].chunk );
				}
				i = scanIndex - 1; // Move to the last scanned index
				continue;
			}

			// Otherwise, encode the count and the chunk
			encoded.Add( (byte)(64 + (GroupedChunks[i].count - 2)) ); // Encode the count to fit in the range 64-255 (Where 2 is the smallest possible value)
			encoded.AddRange( GroupedChunks[i].chunk );
		}

		return encoded;
	}

	public static IEnumerable<byte> RunLengthDecodeBy( this IEnumerable<byte> data, int stride )
	{
		if ( stride <= 0 )
		{
			throw new ArgumentException( "Stride must be greater than 0", nameof( stride ) );
		}

		var decoded = new List<byte>();
		var dataList = data.ToList();
		for ( int i = 0; i < dataList.Count; )
		{
			if ( dataList[i] < 64 )
			{
				// Non-repeat span
				int count = dataList[i] + 1; // Count is 0-63, so we add 1
				i++;
				while ( count > 0 )
				{
					for ( int j = 0; j < stride; j++ )
					{
						if ( i >= dataList.Count )
						{
							Log.Warning( "RunLengthDecodeBy: Attempted to read beyond the end of the data list." );
							break; // Prevent out of bounds
						}
						decoded.Add( dataList[i] );
						i++;
					}
					count--;
				}
			}
			else
			{
				int count = (dataList[i] - 64) + 2; // Decode the count from the range 64-255 (Where 2 is the smallest possible value)
				List<byte> chunk = new List<byte>();
				i++;
				for ( int j = 0; j < stride && i < dataList.Count; j++ )
				{
					chunk.Add( dataList[i] );
					i++;
				}
				for ( int j = 0; j < count; j++ )
				{
					decoded.AddRange( chunk );
				}
			}
		}
		return decoded;
	}

	public static IEnumerable<float> Components( this Vector3 vector )
	{
		yield return vector.x;
		yield return vector.y;
		yield return vector.z;
	}

	public static IEnumerable<int> Components( this Vector3Int vector )
	{
		yield return vector.x;
		yield return vector.y;
		yield return vector.z;
	}

	public static Vector3 VoxelToWorld( Vector3 position )
	{
		return position * World.BlockScale;
	}

	public static Vector3Int WorldToVoxel( Vector3 position )
	{
		return (position / World.BlockScale).Floor();
	}

	public static bool Within( this Vector3Int pos, Vector3Int mins, Vector3Int maxes )
	{
		return pos.x >= mins.x && pos.y >= mins.y && pos.z >= mins.z
			&& pos.x <= maxes.x && pos.y <= maxes.y && pos.z <= maxes.z;
	}

	public static Panel FindChild( this Panel parent, string condition )
	{
		if ( parent == null )
		{
			return null;
		}

		var predicate = condition.StartsWith( "#" )
			? (Func<Panel, bool>)(x => x.Id == condition.Substring( 1 ))
			: (Func<Panel, bool>)(x => x.HasClass( condition ));
		return parent.Children.FirstOrDefault( predicate ) ??
			   parent.Children.Select( child => child.FindChild( condition ) ).FirstOrDefault( x => x != null );
	}

	public static T ParentOfType<T>( this Panel p ) where T : Panel
	{
		var parent = p.Parent;
		while ( parent is Panel and not T )
		{
			parent = parent.Parent;
		}
		return parent as T;
	}

	public static Panel RootPanel( this Panel p )
	{
		var parent = p;
		while ( parent is not null and not Sandbox.UI.RootPanel )
		{
			parent = parent.Parent;
		}
		return parent;
	}
}
