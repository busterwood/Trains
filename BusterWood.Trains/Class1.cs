using BusterWood.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BusterWood.Trains
{
    interface IReserveTrains
    {
        ReservationResult Reserve(ReservationRequest attempt);
    }

    struct ReservationRequest
    {
        public TrainId TrainId { get; }
        public string BookingRef { get; }
        public IReadOnlySet<SeatId> Seats { get; }

        public ReservationRequest(TrainId trainId, string bookingRef, UniqueList<SeatId> seats)
        {
            TrainId = trainId;
            BookingRef = bookingRef;
            Seats = seats;
        }
    }

    interface IFindTheBestSeats
    {
        ReservationResult Reserve(SeatRequest seats);
    }

    struct SeatRequest
    {
        public TrainId TrainId { get; }
        public string BookingRef { get; }
        public int NumberOfSeats { get; }

        public SeatRequest(TrainId trainId, string bookingRef, int seats)
        {
            TrainId = trainId;
            BookingRef = bookingRef;
            NumberOfSeats = seats;
        }

        public override string ToString() => $"Booking: '{BookingRef}', Train: '{TrainId}', Seats: {NumberOfSeats}";
    }
    
    /// <remarks>Struc because not nullable</remarks>
    struct ReservationResult
    {
        public TrainId TrainId { get; }
        public string BookingRef { get; }
        public UniqueList<SeatId> Seats { get; }

        public ReservationResult(TrainId trainId, string bookingRef, UniqueList<SeatId> seats)
        {
            TrainId = trainId;
            BookingRef = bookingRef;
            Seats = seats;
        }

        public bool Successful => !string.IsNullOrEmpty(BookingRef);
    }

    interface IFindTrains
    {
        UniqueList<TrainId> FindTrains(DateTime date);
        TrainTopology GetTopology(TrainId trainId);
    }

    class TrainTopology
    {
        public TrainId TrainId { get; }
        public UniqueList<Seat> Seats { get; }
    }

    struct Seat
    {
        public SeatId SeatId { get; }
        public string BookingRef { get; }

        public bool IsFree => string.IsNullOrEmpty(BookingRef);
    }

    struct SeatId
    {
        public string Number { get; }
        public string Coach { get; }

        public SeatId(string id)
        {
            if (id == null)
                throw new ArgumentNullException(nameof(id));
            if (id.Length < 2)
                throw new ArgumentException(nameof(id));
            var len = id.Length;
            Number = Coach = id.Substring(0, len - 1);
            Coach = id.Substring(len - 1);
        }

        public override string ToString() => $"{Number}{Coach}";
    }

    struct TrainId
    {
        readonly string id;

        public TrainId(string id)
        {
            this.id = id;
        }

        public override string ToString() => id;
    }


    class FindTheBestSeats : IFindTheBestSeats
    {
        readonly IFindTrains trainFinder;
        readonly IReserveTrains reservations;

        public FindTheBestSeats(IFindTrains findTrains, IReserveTrains reservations)
        {
            this.trainFinder = findTrains;
            this.reservations = reservations;
        }

        public ReservationResult Reserve(SeatRequest request)
        {
            var train = trainFinder.GetTopology(request.TrainId);
            if (!train.Seats.ReservationAllowed())
                return ReservationFailed(request.TrainId);

            var coaches = GroupByCoach(train);

            foreach (var possibility in PossibleReservations(coaches, request.NumberOfSeats))
            {
                var result = reservations.Reserve(new ReservationRequest(request.TrainId, request.BookingRef, possibility));
                if (result.Successful)
                    return result;
            }

            return ReservationFailed(request.TrainId);
        }

        static ReservationResult ReservationFailed(TrainId trainId) => new ReservationResult(trainId, "", new UniqueList<SeatId>());

        static IEnumerable<UniqueList<Seat>> GroupByCoach(TrainTopology train)
        {
            return train.Seats
                .GroupBy(s => s.SeatId.Coach)
                .Select(grp => grp.OrderBy(s => s.SeatId.Number).ToUniqueList());
        }

        static IEnumerable<UniqueList<SeatId>> PossibleReservations(IEnumerable<UniqueList<Seat>> coaches, int numberOfSeats)
        {            
            return coaches
                .Where(coach => coach.ReservationAllowed()) // coach has enough free seats
                .Select(coach => coach.FindFreeSeats(numberOfSeats)) // find seats, add booking ref
                .Where(seats => seats.Count == numberOfSeats); // where the whole party will fit on the coach
        }

    }

    static class BusinessExtensions
    {
        public static bool ReservationAllowed(this IReadOnlySet<Seat> seats)
        {
            var emptySeats = seats.Where(s => string.IsNullOrEmpty(s.BookingRef)).Count();
            return ((emptySeats * 100) / seats.Count) < 70;
        }

        public static UniqueList<SeatId> FindFreeSeats(this IReadOnlySet<Seat> seats, int number)
        {
            return seats.Where(s => s.IsFree).Take(number).Select(s => s.SeatId).ToUniqueList(); // todo: adjasent seats
        }
    }
}
