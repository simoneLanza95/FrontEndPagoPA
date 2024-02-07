﻿using System.ComponentModel.DataAnnotations.Schema;

namespace FrontEndPagoPA.Models
{
    public class GetRendicontazioniResult
    {
        public string anagraficaPagatore { get; set; }
        public string codiceIdentificativoUnivocoPagatore { get; set; }
        public string iuv { get; set; }
        public decimal? price { get; set; }
        public int numeroRata { get; set; }
        public DateTime expirationDate { get; set; }
        public bool? paid { get; set; }
    }
}
