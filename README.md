# 25x25-Sudoku
This puzzle is considerably more difficult than a classic sudoku puzzle like https://www.codingame.com/training/medium/sudoku-solver. Simple backtracking will not be enough, knowing that there can be more than 300 unrevealed cells.

# DLX Sudoku Solver (25×25)

A C# implementation of Knuth’s Dancing Links (DLX) algorithm to solve 25×25 Sudoku puzzles (letters A–Y) by modeling them as exact-cover problems. In practice, this solver completes a full 25×25 in milliseconds.

---

## Table of Contents

- [Background](#background)
- [Exact-Cover Modeling](#exact-cover-modeling)
- [Dancing Links (DLX)](#dancing-links-dlx)
- [Implementation Details](#implementation-details)
  - [Data Structures](#data-structures)
  - [Building the Matrix](#building-the-matrix)
  - [Search Procedure](#search-procedure)
- [Complexity Analysis](#complexity-analysis)

---

## Background

A Sudoku of size N×N (here N=25) requires filling each cell with a symbol (A–Y) so that each row, column, and each 5×5 subgrid contains every symbol exactly once. This can be formulated as an exact-cover problem:

1. **Cell constraint**: each of the N² cells must be filled exactly once.  
2. **Row–symbol constraint**: each row must include each symbol exactly once.  
3. **Column–symbol constraint**: each column must include each symbol exactly once.  
4. **Box–symbol constraint**: each 5×5 box must include each symbol exactly once.  

Each assignment of a symbol to a cell covers one of each constraint, forming the options (rows) in an exact-cover matrix.

## Exact-Cover Modeling

- **Columns** (constraints): 4 × N² = 4 × 625 = 2500.  
- **Rows** (options): N² × N = 625 × 25 = 15625, each with exactly four 1s.  

We build a sparse binary matrix (15625×2500), then solve for an exact cover.

## Dancing Links (DLX)

DLX uses a toroidal, doubly-linked data structure:

- **Column headers**: track remaining nodes (`Size`) and link in a circular list via a master header.  
- **Nodes**: linked L↔R (rows) and U↔D (columns), each pointing to its column header.  
- **Cover/Uncover**: operations remove/restore columns and associated rows by updating pointers in O(1) time per link.  

The recursive search chooses at each step the column with the fewest nodes (minimum remaining values heuristic), covers it, and explores each row in that column until a full solution (all columns covered) is found.

## Implementation Details

### Data Structures

```csharp
public class Node {
    public Node L, R, U, D;
    public Column C;
    public int Row, Col, Digit;
    // Constructor links node to itself
}

public class Column : Node {
    public int Size;
    public string Name;
    public Column(string name) { C = this; Name = name; Size = 0; }
}
```

The `DLX` class maintains:
- A master `header` column  
- An array `columns[]` of all constraint headers  
- A `List<Node> solution` for the current partial solution  

### Building the Matrix

1. **Initialize DLX** with `colCount = 4 * N * N`.  
2. **Parse input**: 25×25 grid, `'.'` for empty or `A–Y` for pre-filled.  
3. **Add rows**:  
   - For each cell `(r,c)`, compute box index `b = (r/5)*5 + (c/5)`.  
   - If empty, loop `d` from 0 to 24; else use the pre-filled `d`.  
   - Compute column indices:  
     ```csharp
     int cellIdx =   r*N + c;
     int rowDig  =   N2  + r*N + d;
     int colDig  = 2*N2  + c*N + d;
     int boxDig  = 3*N2  + b*N + d;
     ```
   - Call  
     ```csharp
     dlx.AddRow(new[]{cellIdx,rowDig,colDig,boxDig}, r, c, d);
     ```

### Search Procedure

```csharp
public bool Search() {
    if (header.R == header) return true; // solved
    // Choose column with smallest Size
    Column c = ChooseMinColumn();
    Cover(c);
    for (Node r = c.D; r != c; r = r.D) {
        solution.Add(r);
        for (Node j = r.R; j != r; j = j.R) Cover(j.C);
        if (Search()) return true;
        // backtrack
        solution.RemoveAt(solution.Count-1);
        for (Node j = r.L; j != r; j = j.L) Uncover(j.C);
    }
    Uncover(c);
    return false;
}
```

- **Cover(Column c)**: unlink `c` from headers; for each node in `c`, unlink its row.  
- **Uncover(Column c)**: reverse of `Cover`, relinking pointers.  

## Complexity Analysis

- **Building**: O(N³) to enumerate ~15625 rows and link each node.  
- **Search**: worst-case exponential, pruned by MRV heuristic and constant-time cover/uncover. Solves 25×25 puzzles in milliseconds on modern machines.
