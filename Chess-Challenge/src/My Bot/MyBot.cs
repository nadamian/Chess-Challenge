using ChessChallenge.API;
using System;
using static System.Math;
using System.ComponentModel.DataAnnotations;

public class MyBot : IChessBot
{
    const int positiveInfinity = 9999999;
    const int negativeInfinity = -positiveInfinity;

    //Bitboard population count constants
    const ulong k1 = 0x5555555555555555;
    const ulong k2 = 0x3333333333333333;
    const ulong k4 = 0x0f0f0f0f0f0f0f0f;
    const ulong kf = 0x0101010101010101;

    //Piece material values
    public int pawnVal = 100;
    public int knightVal = 300;
    public int BishopVal = 320;
    public int RookVal = 500; 
    public int QueenVal = 900;

    public int baseDepth = 3;
    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();
        int bestScore = int.MinValue;
        int bestMoveIndex = 0;
        for (int i = 0; i < moves.Length; i++)
        {
            board.MakeMove(moves[i]);
            int evaluation = -Search(board, baseDepth, negativeInfinity, positiveInfinity);
            board.UndoMove(moves[i]);
            if (evaluation > bestScore)
            {
                bestScore = evaluation;
                bestMoveIndex = i;
            }
        }
        Console.WriteLine(bestScore);
        Console.WriteLine(moves[bestMoveIndex]);
        return moves[bestMoveIndex];
    }

    //Evaluates the position based on material and piece mobility
    public int Evaluate(Board board)
    {
        if (board.IsInCheckmate())
        {
            return negativeInfinity;
        }
        if (board.IsDraw())
        {
            return 0;
        }
        bool currentTurn = board.IsWhiteToMove; //True if white's move false if black's
        int material = CountMaterial(board, currentTurn);
        int mobilityScore = MobilityScore(board);
        return material + mobilityScore;
    }

    public int Search(Board board, int depth, int alpha, int beta)
    {
        if (depth == 0)
        {
            return Evaluate(board);
        }
        if (board.IsInCheckmate())
        {
            return negativeInfinity - depth; //scuffed way to get it to prioritize mate in fewer moves
        }
        if (board.IsDraw())
        {
            return 0;
        }
        Move[] moves = board.GetLegalMoves();
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int evaluation = -Search(board, depth - 1, -beta, -alpha);
            board.UndoMove(move);
            if(evaluation >= beta)
            {
                return beta;
            }
            alpha = Max(alpha, evaluation);
        }
        return alpha;
    }

    //Counts white and black material and reports results with respect to the player whose turn it is. 
    public int CountMaterial(Board board, bool turn)
    {
        PieceList[] pieces = board.GetAllPieceLists();
        int whiteMaterial = pieces[0].Count * pawnVal + pieces[1].Count * knightVal + pieces[2].Count *
                BishopVal + pieces[3].Count * RookVal + pieces[4].Count * QueenVal;
        int blackMaterial = pieces[6].Count * pawnVal + pieces[7].Count * knightVal + pieces[8].Count *
            BishopVal + pieces[9].Count * RookVal + pieces[10].Count * QueenVal;
        return turn ? whiteMaterial - blackMaterial : blackMaterial - whiteMaterial;
    }

    int MobilityScore(Board board)
    {
        int whiteScore = 0;
        int blackScore = 0;
        PieceList[] pieces = board.GetAllPieceLists();
        ulong blockers = board.AllPiecesBitboard;
        for (int i = 0; i < pieces.Length; i++)
        {
            PieceList pieceTypeMoves = pieces[i];
            ulong attackMoves = 0;
            foreach (Piece piece in pieceTypeMoves)
            {
                if (i == 0 || i == 6)
                {
                    attackMoves = attackMoves | BitboardHelper.GetPawnAttacks(piece.Square, piece.IsWhite);
                }
                else if (i == 1 || i == 7)
                {
                    attackMoves = attackMoves | BitboardHelper.GetKnightAttacks(piece.Square);
                }
                else if (i == 2 || i == 8)
                {
                    attackMoves = attackMoves | BitboardHelper.GetSliderAttacks(PieceType.Bishop, piece.Square, blockers);
                }
                else if (i == 3 || i == 9)
                {
                    attackMoves = attackMoves | BitboardHelper.GetSliderAttacks(PieceType.Rook, piece.Square, blockers);
                }
                else if (i == 4 || i == 10)
                {
                    attackMoves = attackMoves | BitboardHelper.GetSliderAttacks(PieceType.Queen, piece.Square, blockers);
                }
            }
            if (i < 6)
            {
                whiteScore += BitboardPopCount(attackMoves);
            }
            else
            {
                blackScore += BitboardPopCount(attackMoves);
            }   
        }
        return board.IsWhiteToMove ? whiteScore * 30 - blackScore : blackScore * 30 - whiteScore;
    }

    /*Bitboard population count algorithm found on chessprogramming wiki. I understand about half
     of this.*/
    static int BitboardPopCount(ulong bitboard)
    {
        ulong x = bitboard - ((bitboard >> 1) & k1);
        x = (x & k2) + ((x >> 2) & k2);
        x = (x + (x >> 4)) & k4;
        x = (x * kf) >> 56;
        return (int)x;
    }
}