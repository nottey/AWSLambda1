using System;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace AWSLambda1.Models
{
    public class SneakerEntity : SneakerModel
    {
        private DataRow row;

        public SneakerEntity()
        {

        }
       public SneakerEntity(SneakerModel model)
        {
            this.Brand = model.Brand;
            this.Model = model.Model;
            this.Name = model.Name;
            this.Type = model.Type;
            this.ID = model.ID;
            this.UPC = model.UPC;
            this.Colorway1 = model.Colorway1;
            this.Colorway2 = model.Colorway2;
            this.ReleaseDate = model.ReleaseDate;
            this.PurchDate = model.PurchDate;
            this.RetailPrice = model.RetailPrice;
            this.ImgSrc = model.ImgSrc;
            this.DbImage = model.DbImage;
            this.RowKey = model.UPC;
        }

        public SneakerEntity(DataRow row)
        {
            this.Brand = row["Brand"].ToString();
            this.Model = row["Model"].ToString();
            this.Name = row["Name"].ToString();
            this.Type = row["Type"].ToString();
            this.ID = row["ID"].ToString();
            this.UPC = row["UPC"].ToString();
            this.Colorway1 = row["Colorway"].ToString();
            this.ReleaseDate = row["ReleaseDate"].ToString();
            this.PurchDate = row["purchaseDate"].ToString();
            this.RetailPrice = (double)row["RetailPrice"];
            this.ImgSrc = row["ImgSrc"].ToString();
            this.InCollection = bool.Parse(row["inCollection"].ToString());
        }

        public string PartitionKey { get; set; } 
        public string RowKey { get; set; } 
    }}
