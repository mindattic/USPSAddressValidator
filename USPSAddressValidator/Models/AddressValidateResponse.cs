﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace USPSAddressValidator.Models
{
    // using System.Xml.Serialization;
    // XmlSerializer serializer = new XmlSerializer(typeof(AddressValidateResponse));
    // using (StringReader reader = new StringReader(xml))
    // {
    //    var test = (AddressValidateResponse)serializer.Deserialize(reader);
    // }

    [XmlRoot(ElementName = "Address")]
    public class Address
    {

        [XmlElement(ElementName = "Address1")]
        public string? Address1 { get; set; }

        [XmlElement(ElementName = "Address2")]
        public string? Address2 { get; set; }

        [XmlElement(ElementName = "City")]
        public string? City { get; set; }

        [XmlElement(ElementName = "CityAbbreviation")]
        public string? CityAbbreviation { get; set; }

        [XmlElement(ElementName = "State")]
        public string? State { get; set; }

        [XmlElement(ElementName = "Zip5")]
        public int Zip5 { get; set; }

        [XmlElement(ElementName = "Zip4")]
        public int Zip4 { get; set; }

        [XmlElement(ElementName = "DeliveryPoint")]
        public int DeliveryPoint { get; set; }

        [XmlElement(ElementName = "CarrierRoute")]
        public string? CarrierRoute { get; set; }

        [XmlElement(ElementName = "Footnotes")]
        public string? Footnotes { get; set; }

        [XmlElement(ElementName = "DPVConfirmation")]
        public string? DPVConfirmation { get; set; }

        [XmlElement(ElementName = "DPVCMRA")]
        public string? DPVCMRA { get; set; }

        [XmlElement(ElementName = "DPVFootnotes")]
        public string? DPVFootnotes { get; set; }

        [XmlElement(ElementName = "Business")]
        public string? Business { get; set; }

        [XmlElement(ElementName = "CentralDeliveryPoint")]
        public string? CentralDeliveryPoint { get; set; }

        [XmlElement(ElementName = "Vacant")]
        public string? Vacant { get; set; }

        [XmlAttribute(AttributeName = "ID")]
        public int ID { get; set; }

        [XmlText]
        public string? Text { get; set; }
    }

    [XmlRoot(ElementName = "AddressValidateResponse")]
    public class AddressValidateResponse
    {

        [XmlElement(ElementName = "Address")]
        public Address? Address { get; set; }
    }


}
