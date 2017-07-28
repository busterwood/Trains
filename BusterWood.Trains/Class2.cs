using BusterWood.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BusterWood.Trains
{
    /// <summary>
    /// Pure business logic for attempting reservation of seats for a party of N people.
    /// </summary>
    /// <remarks>
    /// Note that this class does no logging and has no dependencies, see <see cref="ActualBestSeatFinder"/> for concreate implementation.
    /// Methods are virtual so subclass can add logging.
    /// </remarks>
    abstract class BestSeatFinder
    {
        public ReservationResult Reserve(SeatRequest request)
        {
            TrainTopology train = GetTrainTopology(request);

            if (TrainWillBeFull(train, request))
                return ReservationFailed(request);

            foreach (var coach in CoachesWithEnoughFreeSeats(train, request))
            {
                var result = AttemptReservation(coach, request);
                if (result.Successful)
                    return result;
            }
            return ReservationFailed(request);
        }

        protected abstract TrainTopology GetTrainTopology(SeatRequest request);

        protected virtual ReservationResult ReservationFailed(SeatRequest request) => new ReservationResult(request.TrainId, "", new UniqueList<SeatId>());

        protected virtual bool TrainWillBeFull(TrainTopology train, SeatRequest request) => IsFull(train.Seats, request.NumberOfSeats);

        protected virtual bool IsFull(UniqueList<Seat> seats, int requestedNumberOfSeats = 0)
        {
            if (seats.Count == 0) return true;
            float freeSeats = seats.Count(s => s.IsFree) - requestedNumberOfSeats;
            return freeSeats / seats.Count > 0.7f;
        }

        protected virtual IEnumerable<UniqueList<SeatId>> CoachesWithEnoughFreeSeats(TrainTopology train, SeatRequest request)
        {
            var coaches = train.Seats
                .GroupBy(s => s.SeatId.Coach)
                .Select(grp => grp.OrderBy(s => s.SeatId.Number).ToUniqueList());

            return coaches
                .Where(coach => !IsFull(coach))                                 // coach has enough free seats
                .Select(coach => FindFreeSeats(coach, request.NumberOfSeats))   // find the free ones
                .Where(seats => seats.Count == request.NumberOfSeats);          // where the whole party will fit on the coach
        }

        protected virtual UniqueList<SeatId> FindFreeSeats(UniqueList<Seat> coach, int numberOfSeats)
        {
            return coach
                .Where(s => s.IsFree)
                .Take(numberOfSeats)
                .Select(s => s.SeatId)
                .ToUniqueList(); 
        }

        protected abstract ReservationResult AttemptReservation(UniqueList<SeatId> seats, SeatRequest request);
    }

    /// <summary>
    /// Implements <see cref="BestSeatFinder"/> with dependencies and adds logging
    /// </summary>
    /// <remarks>
    /// Overrides abstract methods to implment service calls. Override virtual methods to add logging.
    /// </remarks>
    class ActualBestSeatFinder : BestSeatFinder
    {
        readonly IFindTrains trainFinder;
        readonly IReserveTrains reservations;

        public ActualBestSeatFinder(IFindTrains trainFinder, IReserveTrains reservations)
        {
            this.trainFinder = trainFinder;
            this.reservations = reservations;
        }

        protected override TrainTopology GetTrainTopology(SeatRequest request) => trainFinder.GetTopology(request.TrainId);

        protected override bool TrainWillBeFull(TrainTopology train, SeatRequest request)
        {
            var full = base.TrainWillBeFull(train, request);
            var msg = full ? "full" : "not full";
            Console.Error.WriteLine($"INFO: {request}, train is {msg}");
            return full;
        }

        protected override IEnumerable<UniqueList<SeatId>> CoachesWithEnoughFreeSeats(TrainTopology train, SeatRequest request)
        {
            var coaches = base.CoachesWithEnoughFreeSeats(train, request);
            Console.Error.WriteLine($"INFO: {request}, there are {coaches.Count()} coaches with enough free space");
            return coaches;
        }

        protected override ReservationResult ReservationFailed(SeatRequest request)
        {
            Console.Error.WriteLine($"INFO: {request}, request failed");
            return base.ReservationFailed(request);
        }

        protected override ReservationResult AttemptReservation(UniqueList<SeatId> seats, SeatRequest request)
        {
            var req = new ReservationRequest(request.TrainId, request.BookingRef, seats);
            var result = reservations.Reserve(req);
            if (result.Successful)
                Console.Error.WriteLine($"INFO: {request}, request succeeded");
            return result;
        }
    }
}
