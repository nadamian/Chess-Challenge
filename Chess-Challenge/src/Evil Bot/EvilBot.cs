using ChessChallenge.API;
using System;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot
    {
        public int pawnVal = 10;
        public int knightVal = 30;
        public int BishopVal = 32;
        public int RookVal = 50;
        public int QueenVal = 90;

        public Move Think(Board board, Timer timer)
        {
            Move[] moves = board.GetLegalMoves();
            bool turn = board.IsWhiteToMove;
            float bestEval = -99999;
            int bestMove = 0;
            for (int i = 0; i < moves.Length; i++)
            {
                board.MakeMove(moves[i]);
                float eval = -Search(board, 2, -99999, 99999, !turn);
                board.UndoMove(moves[i]);
                if (eval > bestEval)
                {
                    bestEval = eval;
                    bestMove = i;
                }
            }
            return moves[bestMove];
        }

        public float Evaluate(Board board, bool turn)
        {
            if (board.IsInCheckmate() && turn)
            {
                return -9999;
            }
            if (board.IsInCheckmate() && !turn)
            {
                return 9999;
            }
            PieceList[] pieces = board.GetAllPieceLists();
            int whiteMaterial = pieces[0].Count * pawnVal + pieces[1].Count * knightVal + pieces[2].Count *
                BishopVal + pieces[3].Count * RookVal + pieces[4].Count * QueenVal;
            int blackMaterial = pieces[6].Count * pawnVal + pieces[7].Count * knightVal + pieces[8].Count *
                BishopVal + pieces[9].Count * RookVal + pieces[10].Count * QueenVal;
            return turn ? whiteMaterial - blackMaterial : blackMaterial - whiteMaterial;
        }

        public float Search(Board board, int depth, float alpha, float beta, bool turn)
        {
            if (depth == 0)
            {
                return Evaluate(board, turn);
            }
            Move[] moves = board.GetLegalMoves();
            float bestEval = -99999;
            foreach (Move move in moves)
            {
                board.MakeMove(move);
                float eval = -Search(board, depth - 1, alpha, beta, !turn);
                bestEval = Math.Max(bestEval, eval);
                board.UndoMove(move);
            }
            return bestEval;
        }
    }
}