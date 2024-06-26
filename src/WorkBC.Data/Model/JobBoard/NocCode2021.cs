﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WorkBC.Data.Model.JobBoard
{
    public class NocCode2021
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        [StringLength(5)]
        public string Code { get; set; }

        [StringLength(150)]
        public string Title { get; set; }

        [StringLength(250)]
        public string FrenchTitle { get; set; }

        [StringLength(30)]
        public string Code2016 { get; set; }
    }
}