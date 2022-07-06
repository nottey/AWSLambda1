using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace AWSLambda1.Models
{
    public class SneakerModel
    {
        public SneakerModel()
        {
        }

        public SneakerModel(SneakerEntity entity)
        {
            this.Brand = entity.Brand;
            this.Model = entity.Model;
            this.Name = entity.Name;
            this.ID = entity.ID;
            this.Type = entity.Type;
            this.UPC = entity.UPC;
            this.Colorway1 = entity.Colorway1;
            this.Colorway2 = entity.Colorway2;
            this.DbImage = entity.DbImage; 
            this.ReleaseDate = entity.ReleaseDate;
            this.PurchDate = entity.PurchDate;
            this.RetailPrice = entity.RetailPrice;
            this.ImgSrc = entity.ImgSrc;
            this.DbImage = entity.DbImage;
            this.InCollection = entity.InCollection;
        }
        [Required]
        public string Brand { get; set; }
        [Required]
        public string Model { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; }
        public string Type { get; set; }
        public string ID { get; set; }
        [Required]
        [StringLength(15)]
        public string UPC { get; set; }
        public string Colorway1 { get; set; }
        public string Colorway2 { get; set; }
        public string ReleaseDate { get; set; }
        public string PurchDate { get; set; }
        public double RetailPrice { get; set; }
        public string ImgSrc { get; set; }
        public bool Raffle { get; set; }
        public bool Featured { get; set; }
        public string Link1 { get; set; }
        public bool InCollection { get; set; }

        [NotMapped]
        public string DbImage { get; set; }  

        [NotMapped]
        public bool isNew { get; set; }
    }
}