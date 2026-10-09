namespace SMC.BhoomiMalmatta.Web.Security
{
    /// <summary>Central comparison for applicant tickets: a ticket grants access only to its one server-bound application id.</summary>
    public static class CitizenTicketAccess
    {
        public static bool Allows(int ticketApplicationId, int requestedApplicationId)
        {
            return ticketApplicationId > 0 && requestedApplicationId > 0 && ticketApplicationId == requestedApplicationId;
        }
    }
}
