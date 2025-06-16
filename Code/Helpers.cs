using System;
using System.Collections.Generic;
using Sandbox;

public static class Helpers {
	/// <summary>
	/// Divides two integers and rounds down to the lowest integer.
	/// </summary>
	/// <param name="a"></param>
	/// <param name="b"></param>
	/// <returns></returns>
	public static int FloorDiv( this int a, int b ) {
		return (int)System.Math.Floor( (double)a / b );
	}

	public static Vector3Int Floor( this Vector3 vector ) {
		return new Vector3Int(
			(int)System.Math.Floor( vector.x ),
			(int)System.Math.Floor( vector.y ),
			(int)System.Math.Floor( vector.z )
		);
	}

	// Modulo and Remainder methods for Vector3
	// a modulo b is defined as (a % b + b) % b to ensure the result is always positive.
	// Where as remainder is simply a % b, which can be negative if a is negative.
	public static Vector3 Modulo( this Vector3 vector, Vector3 mod ) {
		return new Vector3(
			((vector.x % mod.x) + mod.x) % mod.x,
			((vector.y % mod.y) + mod.y) % mod.y,
			((vector.z % mod.z) + mod.z) % mod.z
		);
	}

	public static Vector3 Remainder( this Vector3 vector, Vector3 mod ) {
		return new Vector3(
			vector.x % mod.x,
			vector.y % mod.y,
			vector.z % mod.z
		);
	}

	public static Vector3 Fractional( this Vector3 vector ) {
		return vector.Modulo( Vector3.One );
	}

	public static IEnumerable<T> TakeRandom<T>( this Random rng, IEnumerable<T> source, int count ) {
		if ( count > source.Count() ) {
			count = source.Count();
		}
		if ( count <= 0 ) {
			yield break; // No items to take
		}

		// We're going to be annoying and take from it multiple times, so it's fastest to convert to a list first.
		// And iterate only once.
		var l = source.ToList();
		var taken = new HashSet<int>();
		for ( int i = 0; i < count; i++ ) {
			int index = rng.Next( l.Count - taken.Count );
			// This takes a random index from the list, but gives 'padding' for already taken indices.
			// In order for this to work, we then need to add 1 to the index for every taken index that is less than the current index.
			// EG. Assume there are 10 elements (0 - 9), and the element '5' was already taken
			// We roll a number between 0 and 8 inclusive
			// If that number is 5,6,7 or 8, we need to add 1 to the index, because 5 is already taken.
			// Which makes the effective range of indicies 0-4 GAP 6-9
			foreach ( var t in taken ) {
				if ( t <= index ) {
					index++;
				}
			}
			// At this point, taken can never contain the index thanks to the above math. (There might be a better way to do this?)
			taken.Add( index );
			yield return l[index];
		}
	}

	public static IEnumerable<float> Components( this Vector3 vector ) {
		yield return vector.x;
		yield return vector.y;
		yield return vector.z;
	}

	public static IEnumerable<int> Components( this Vector3Int vector ) {
		yield return vector.x;
		yield return vector.y;
		yield return vector.z;
	}
}
