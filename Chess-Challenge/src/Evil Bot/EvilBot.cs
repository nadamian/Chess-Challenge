using ChessChallenge.API;
using System;
using System.Linq;
using static System.Math;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot
    {
        const int positiveInfinity = 9999999;
        const int negativeInfinity = -positiveInfinity;
        int baseDepth = 3;

        public Move Think(Board board, Timer timer)
        {
            Move[] moves = board.GetLegalMoves();
            MoveOrder(board, moves);
            int bestScore = negativeInfinity;
            int bestMoveIndex = 0;
            bool lowtime = timer.MillisecondsRemaining < timer.OpponentMillisecondsRemaining &&
                timer.MillisecondsRemaining < 10000;
            if (lowtime)
            {
                baseDepth = 3;
            }
            for (int i = 0; i < moves.Length; i++)
            {
                board.MakeMove(moves[i]);
                //Hard coding depth 3 for now which pains me but it saves brain power
                int evaluation = -Search(board, baseDepth, negativeInfinity, positiveInfinity, lowtime, board.IsWhiteToMove);
                if (board.GameRepetitionHistory.Contains(board.ZobristKey))
                {
                    evaluation -= 100;
                }
                board.UndoMove(moves[i]);
                if (evaluation > bestScore)
                {
                    bestScore = evaluation;
                    bestMoveIndex = i;
                }
            }
            return moves[bestMoveIndex];
        }

        //Evaluates the position based on material and piece mobility
        public int Evaluate(Board board, bool lowtime, bool turn)
        {
            PieceList[] pieces = board.GetAllPieceLists();
            int whiteMaterial = pieces[0].Count * 100 + pieces[1].Count * 300 + pieces[2].Count *
                    320 + pieces[3].Count * 500 + pieces[4].Count * 900;
            int blackMaterial = pieces[6].Count * 100 + pieces[7].Count * 300 + pieces[8].Count *
                320 + pieces[9].Count * 500 + pieces[10].Count * 900;
            int material = turn ? whiteMaterial - blackMaterial : blackMaterial - whiteMaterial;
            if (lowtime)
            {
                return material;
            }
            //int material = CountMaterial(board, currentTurn);
            //int mobilityScore = MobilityScore(board);
            int whiteScore = 0;
            int blackScore = 0;
            ulong blockers = board.AllPiecesBitboard;
            //Gives bonuses for pieces having extra mobility
            //Can probably get away with rewriting this to give piece specific bonuses.
            for (int i = 0; i < pieces.Length; i++)
            {
                int score = 0;
                foreach (Piece piece in pieces[i])
                {
                    if (i == 0 || i == 6)
                    {
                        //attackMoves = attackMoves | BitboardHelper.GetPawnAttacks(piece.Square, piece.IsWhite);
                        score += BitboardPopCount(BitboardHelper.GetPawnAttacks(piece.Square, piece.IsWhite)) * 10;
                    }
                    else if (i == 1 || i == 7)
                    {
                        //attackMoves = attackMoves | BitboardHelper.GetKnightAttacks(piece.Square);
                        score += BitboardPopCount(BitboardHelper.GetKnightAttacks(piece.Square)) * 25;
                    }
                    else if (i == 2 || i == 8)
                    {
                        //attackMoves = attackMoves | BitboardHelper.GetSliderAttacks(PieceType.Bishop, piece.Square, blockers);
                        score += BitboardPopCount(BitboardHelper.GetSliderAttacks(PieceType.Bishop,
                            piece.Square, blockers)) * 25;
                    }
                    else if (i == 3 || i == 9)
                    {
                        //attackMoves = attackMoves | BitboardHelper.GetSliderAttacks(PieceType.Rook, piece.Square, blockers);
                        score += BitboardPopCount(BitboardHelper.GetSliderAttacks(PieceType.Rook,
                                                   piece.Square, blockers)) * 15;
                    }
                    else if (i == 4 || i == 10)
                    {
                        //attackMoves = attackMoves | BitboardHelper.GetSliderAttacks(PieceType.Queen, piece.Square, blockers);
                        score += BitboardPopCount(BitboardHelper.GetSliderAttacks(PieceType.Queen,
                                                   piece.Square, blockers)) * 15;
                    }
                }
                if (i < 6)
                {
                    //whiteScore += BitboardPopCount(attackMoves);
                    whiteScore += score;
                }
                else
                {
                    //blackScore += BitboardPopCount(attackMoves);
                    blackScore += score;
                }
            }
            ulong whiteKing = board.GetPieceBitboard(PieceType.King, true);
            ulong blackKing = board.GetPieceBitboard(PieceType.King, false);
            int blackKingScore = KingSafetyBonus(blackKing);
            int whiteKingScore = KingSafetyBonus(whiteKing);
            if ((whiteMaterial + blackMaterial) > 2000)
            {
                baseDepth = 3;
                blackKingScore = -blackKingScore;
                whiteKingScore = -whiteKingScore;
            }
            else
            {
                baseDepth = 5;
            }
            int mobilityScore = turn ? whiteScore - blackScore : blackScore - whiteScore;
            int kingSafetyScore = turn ? (whiteKingScore - blackKingScore) * 50 : (blackKingScore -
                whiteKingScore) * 50;
            return material + mobilityScore + kingSafetyScore;
        }

        public int Search(Board board, int depth, int alpha, int beta, bool lowtime, bool turn)
        {
            if (depth == 0)
            {
                return Evaluate(board, lowtime, turn);
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
            MoveOrder(board, moves);
            foreach (Move move in moves)
            {
                board.MakeMove(move);
                int evaluation = -Search(board, depth - 1, -beta, -alpha, lowtime, !turn);
                board.UndoMove(move);
                if (evaluation >= beta)
                {
                    return beta;
                }
                alpha = Max(alpha, evaluation);
            }
            return alpha;
        }

        //Orders moves to optimize alpha beta pruning
        void MoveOrder(Board board, Move[] moves)
        {
            int[] scores = new int[moves.Length];
            bool whiteToMove = board.IsWhiteToMove;
            for (int i = 0; i < moves.Length; i++)
            {
                Move move = moves[i];
                int scoreGuess = 0;
                //Prioritizes high value captures
                if (move.IsCapture)
                {
                    scoreGuess += 100; //* (GetPieceValueFromType(move.CapturePieceType) - 
                                       //GetPieceValueFromType(move.MovePieceType));
                }
                //Prioritizes pawn promotions
                if (move.IsPromotion)
                {
                    scoreGuess += 900; //GetPieceValueFromType(move.PromotionPieceType);
                }
                //Avoids moving to squares attacked by enemy pawns
                ulong pawnAttackBitboard = BitboardHelper.GetPawnAttacks(move.TargetSquare, whiteToMove);
                ulong enemyPawnBitboard = board.GetPieceBitboard(PieceType.Pawn, !whiteToMove);
                if ((pawnAttackBitboard & enemyPawnBitboard) != 0)
                {
                    scoreGuess -= 100; //GetPieceValueFromType(move.MovePieceType);
                }
                //Prioritizes giving check
                /*ulong pieceAttackBitboard = 0;
                ulong allPieces = board.AllPiecesBitboard;
                ulong enemyKing = board.GetPieceBitboard(PieceType.King, !whiteToMove);
                if (move.MovePieceType == PieceType.Pawn)
                {
                    pieceAttackBitboard = BitboardHelper.GetPawnAttacks(move.TargetSquare, whiteToMove);
                }
                else if (move.MovePieceType == PieceType.Knight)
                {
                    pieceAttackBitboard = BitboardHelper.GetKnightAttacks(move.TargetSquare);
                }
                else if (move.MovePieceType == PieceType.Bishop)
                {
                    pieceAttackBitboard = BitboardHelper.GetSliderAttacks(PieceType.Bishop, move.TargetSquare, allPieces);
                }
                else if (move.MovePieceType == PieceType.Rook)
                {
                    pieceAttackBitboard = BitboardHelper.GetSliderAttacks(PieceType.Rook, move.TargetSquare, allPieces);
                }
                else if (move.MovePieceType == PieceType.Queen)
                {
                    pieceAttackBitboard = BitboardHelper.GetSliderAttacks(PieceType.Queen, move.TargetSquare, allPieces);
                }
                if ((pieceAttackBitboard & enemyKing) != 0)
                {
                    scoreGuess += 100;
                }*/
                scores[i] = scoreGuess;
            }
            SortMoves(moves, scores);
        }

        /*Bitboard population count algorithm found on chessprogramming wiki. I understand about half
         of this but it seems to be working.*/
        static int BitboardPopCount(ulong bitboard)
        {
            ulong x = bitboard - ((bitboard >> 1) & 0x5555555555555555);
            x = (x & 0x3333333333333333) + ((x >> 2) & 0x3333333333333333);
            x = (x + (x >> 4)) & 0x0f0f0f0f0f0f0f0f;
            x = (x * 0x0101010101010101) >> 56;
            return (int)x;
        }

        void SortMoves(Move[] moves, int[] scores)
        {
            for (int i = 0; i < moves.Length - 1; i++)
            {
                for (int j = i + 1; j > 0; j--)
                {
                    int swapIndex = j - 1;
                    if (scores[swapIndex] < scores[j])
                    {
                        (moves[j], moves[swapIndex]) = (moves[swapIndex], moves[j]);
                        (scores[j], scores[swapIndex]) = (scores[swapIndex], scores[j]);
                    }
                }
            }
        }

        int KingSafetyBonus(ulong king)
        {
            int verticalScore = 0;
            ulong kingUp = king;
            ulong kingDown = king;
            for (int j = 0; j < 8; j++)
            {
                kingUp = kingUp << 8;
                kingDown = kingDown >> 8;
                if (kingUp == 0 || kingDown == 0)
                {
                    verticalScore = j;
                    break;
                }
            }
            return verticalScore;
        }

        /*int GetPieceValueFromType(PieceType type)
      {
          if (type == PieceType.Pawn)
          {
              return pawnVal;
          }
          else if (type == PieceType.Knight)
          {
              return knightVal;
          }
          else if (type == PieceType.Bishop)
          {
              return BishopVal;
          }
          else if (type == PieceType.Rook)
          {
              return RookVal;
          }
          else if (type == PieceType.Queen)
          {
              return QueenVal;
          }
          else
          {
              return 0;
          }
      }*/

        /*Material and mobility scoring methods commented out because suffing them into Evaluate() is 
         more brain power efficient even if it's uglier*/

        //Counts white and black material and reports results with respect to the player whose turn it is. 
        /*public int CountMaterial(Board board, bool turn)
        {
            PieceList[] pieces = board.GetAllPieceLists();
            int whiteMaterial = pieces[0].Count * pawnVal + pieces[1].Count * knightVal + pieces[2].Count *
                    BishopVal + pieces[3].Count * RookVal + pieces[4].Count * QueenVal;
            int blackMaterial = pieces[6].Count * pawnVal + pieces[7].Count * knightVal + pieces[8].Count *
                BishopVal + pieces[9].Count * RookVal + pieces[10].Count * QueenVal;
            return turn ? whiteMaterial - blackMaterial : blackMaterial - whiteMaterial;
        }*/

        //Scores the position based on the number of squares attacked by each side
        /*int MobilityScore(Board board)
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
        }*/
    }
}