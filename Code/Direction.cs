using Sandbox;

// X+ is North, X- is South, Y+ is Right, Y- is Left, Z+ is Up, Z- is Down
public enum Direction
{
	None,
	North,
	South,
	East,
	West,
	Up,
	Down,
	Forward = North, // Alias for North
	Backward = South, // Alias for South
	Left = West, // Alias for West
	Right = East // Alias for East
}

public static class Directions
{
	public static List<Direction> All { get; } = new List<Direction> {
		Direction.North,
		Direction.South,
		Direction.East,
		Direction.West,
		Direction.Up,
		Direction.Down
	};

	public static Vector3Int Forward( this Direction direction )
	{
		return direction switch
		{
			Direction.None => new Vector3Int( 0, 0, 0 ),
			Direction.North => new Vector3Int( 1, 0, 0 ),
			Direction.South => new Vector3Int( -1, 0, 0 ),
			Direction.East => new Vector3Int( 0, -1, 0 ),
			Direction.West => new Vector3Int( 0, 1, 0 ),
			Direction.Up => new Vector3Int( 0, 0, 1 ),
			Direction.Down => new Vector3Int( 0, 0, -1 ),
			_ => throw new System.ArgumentOutOfRangeException( nameof( direction ), direction, null )
		};
	}

	// Up relative to the face. This is z+ for NSEW, and x+ for Up/Down.
	public static Vector3Int Up( this Direction direction )
	{
		return direction switch
		{
			Direction.None => new Vector3Int( 0, 0, 0 ),
			Direction.North => new Vector3Int( 0, 0, 1 ),
			Direction.South => new Vector3Int( 0, 0, 1 ),
			Direction.East => new Vector3Int( 0, 0, 1 ),
			Direction.West => new Vector3Int( 0, 0, 1 ),
			Direction.Up => new Vector3Int( -1, 0, 0 ), // Up is x+ for Up/Down
			Direction.Down => new Vector3Int( 1, 0, 0 ), // Down is x- for Up/Down
			_ => throw new System.ArgumentOutOfRangeException( nameof( direction ), direction, null )
		};
	}

	// Right relative to the face. This is one counter-clockwise turn from Forward for NSEW, Y+ for Up, and Y- for Down.
	public static Vector3Int Right( this Direction direction )
	{
		return direction switch
		{
			Direction.None => new Vector3Int( 0, 0, 0 ),
			Direction.North => new Vector3Int( 0, 1, 0 ),
			Direction.South => new Vector3Int( 0, -1, 0 ),
			Direction.East => new Vector3Int( 1, 0, 0 ),
			Direction.West => new Vector3Int( -1, 0, 0 ),
			Direction.Up => new Vector3Int( 0, 1, 0 ), // Up is Y+ for Up/Down
			Direction.Down => new Vector3Int( 0, 1, 0 ), // Down is Y- for Up/Down
			_ => throw new System.ArgumentOutOfRangeException( nameof( direction ), direction, null )
		};
	}

	public static Direction Flip( this Direction direction )
	{
		return direction switch
		{
			Direction.None => Direction.None,
			Direction.North => Direction.South,
			Direction.South => Direction.North,
			Direction.East => Direction.West,
			Direction.West => Direction.East,
			Direction.Up => Direction.Down,
			Direction.Down => Direction.Up,
			_ => throw new System.ArgumentOutOfRangeException( nameof( direction ), direction, null )
		};
	}

	public static Direction FromVector( Vector3 vector )
	{
		// Get the direction this vector is most like.
		var absX = System.Math.Abs( vector.x );
		var absY = System.Math.Abs( vector.y );
		var absZ = System.Math.Abs( vector.z );
		if ( absX == 0 && absY == 0 && absZ == 0 )
		{
			return Direction.None; // No direction if the vector is zero
		}
		if ( absX >= absY && absX >= absZ )
		{
			return vector.x > 0 ? Direction.North : Direction.South;
		}
		else if ( absY >= absX && absY >= absZ )
		{
			return vector.y > 0 ? Direction.East : Direction.West;
		}
		else
		{
			return vector.z > 0 ? Direction.Up : Direction.Down;
		}
	}
}
