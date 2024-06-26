﻿using AutoMapper;
using CsvHelper;
using CsvHelper.Configuration;
using FrontEndPagoPA.Models;
using FrontEndPagoPA.Service;
using FrontEndPagoPA.ViewModel;
using log4net;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;



namespace FrontEndPagoPA.Controllers
{
    public class ActionController : Controller
    {
        private readonly TokenProvider _tokenProvider;
        private readonly ActionService _actionService;
        private readonly IMemoryCache _cache;
        private static readonly ILog _logger = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IMapper _mapper;


        public ActionController(TokenProvider tokenProvider, ActionService actionService, IMemoryCache cache, IWebHostEnvironment webHostEnvironment, IMapper mapper)
        {
            _tokenProvider = tokenProvider;
            _actionService = actionService;
            _cache = cache;
            _mapper = mapper;
            _webHostEnvironment = webHostEnvironment;
        }


        public ActionResult RequestComplete()
        {
            return View();
        }

        public IActionResult RichiesteInAttesa()
        {
            return View();
        }

        public async Task<string> GetRendicontazionePagamenti(int page = 1, int itemsPerPage = 100, bool paid = true, string nominativo = "", string dataInizio = "", string dataFine = "", string iuv = "", string codiceFiscale = "", string importoMin = "", string importoMax = "", bool payable = false)
        {
            Globals g = new(_tokenProvider);
            TokenDto token;
            token = g.GetDeserializedToken();
            string today = DateTime.Today.ToString();
            var dataI = new DateTime();
            var dataF = new DateTime();
            string append = "";

            if (nominativo != "" && nominativo != null)
                append += "&nominativo=" + nominativo;

            if (iuv != "" && iuv != null)
                append += "&iuv=" + iuv;

            if (codiceFiscale != "" && codiceFiscale != null)
                append += "&codiceFiscale=" + codiceFiscale;

            if (importoMin != null && importoMin != "")
                append += "&importoMin=" + importoMin;

            if (importoMax != null && importoMax != "")
                append += "&importoMax=" + importoMax;

            if (dataInizio != "" && dataInizio != null)
            {
                dataI = DateTime.Parse(dataInizio);
                append += "&dataInizio=" + dataI;
                today = "";
            }

            if (dataFine != "" && dataFine != null)
            {
                dataF = DateTime.Parse(dataFine);
                append += "&dataFine=" + dataF;
                today = "";
            }

            append += "&payable=" + payable;

            ResponseDto? response = await _actionService.GetInstallmentsAsync(token.sub, true, today, paid, true, page, itemsPerPage, append);

            if (response is not null && response.IsSuccess)
                _cache.Set("rendicontazionePagamenti", JsonConvert.DeserializeObject<List<GetRendicontazioniResult>>(Convert.ToString(response.Result)!)!);
            else
                _logger.Error(response?.Message);

            return JsonConvert.SerializeObject(response);
        }

        public async Task<string?> UploadFileCsv(IFormCollection data)
        {
            var bollettino = HttpContext.Request.Query["bollettino"].ToString();

            bool bulletin = true;
            if (bollettino != "1")
                bulletin = false;

            if (data.Files.Count > 0)
            {
                var upload = data.Files[0];
                if (upload != null && upload.Length > 0)
                {
                    string webRootPath = _webHostEnvironment.WebRootPath;
                    var fileName = DateTime.Now.Ticks + Path.GetExtension(upload.FileName);
                    var directory = webRootPath + Globals.FolderCsv + fileName;
                    using (var fileSrteam = new FileStream(directory, FileMode.Create))
                    {
                        await upload.CopyToAsync(fileSrteam);
                    }

                    var r = ConvertCsv(Globals.FolderCsv + fileName, bulletin);

                    _cache.Set("list", r);

                    return JsonConvert.SerializeObject(r);
                }
            }

            List<CsvDto> records = new();
            return JsonConvert.SerializeObject(records);
        }

        public async Task<string?> UploadFileZip(IFormCollection data)
        {
            if (data.Files.Count > 0)
            {
                var upload = data.Files[0];
                if (upload != null && upload.Length > 0)
                {
                    string webRootPath = _webHostEnvironment.WebRootPath;
                    var fileName = DateTime.Now.Ticks + Path.GetExtension(upload.FileName);
                    var directory = webRootPath + Globals.FolderZip + fileName;
                    using (var fileSrteam = new FileStream(directory, FileMode.Create))
                    {
                        await upload.CopyToAsync(fileSrteam);
                    }

                    var r = OpenZipFile(Globals.FolderZip + fileName);

                    var l = (List<CsvDtoOut>)_cache.Get("list")!;

                    var newList = new List<CsvDtoIn>();
                    foreach (var c in l)
                    {
                        var nc = _mapper.Map<CsvDtoIn>(c);
                        var last = r.FirstOrDefault(a => a.Contains(c.nomeFile!) || a == c.nomeFile);
                        if (last == null)
                        {
                            nc.valid = false;
                            nc.message = "Nessun file collegato a questo nominativo";
                        }
                        else
                        {
                            nc.nomeFile = c.nomeFile;

                            //CONVERTE IN BASE 64
                            nc.inputBase64File = Globals.ConvertFileToBase64(webRootPath + last);

                        }

                        newList.Add(nc);
                    }

                    //ELIMINA FILE ZIP
                    System.IO.File.Delete(directory);
                    System.IO.File.Delete(directory.Replace(".zip", ""));

                    _cache.Set("list", newList);

                    return JsonConvert.SerializeObject(newList);
                }
            }

            List<CsvDto> records = new();
            return JsonConvert.SerializeObject(records);
        }

        public async Task<string> GetRichiesteEsitate(int page = 1, int itemsPerPage = 100, string codiceFiscale = "", string iuv = "", string dataInizio = "", string dataFine = "", string importoMin = "", string importoMax = "")
        {
            Globals g = new(_tokenProvider);
            TokenDto token;
            token = g.GetDeserializedToken();
            string today = DateTime.Today.ToString();
            string append = "";
            var dataI = new DateTime();
            var dataF = new DateTime();

            if (codiceFiscale != "" && codiceFiscale != null)
                append += "&codiceFiscale=" + codiceFiscale;

            if (iuv != "" && iuv != null)
                append += "&iuv=" + iuv;

            if (dataInizio != "" && dataInizio != null)
            {
                dataI = DateTime.Parse(dataInizio);
                append += "&dataInizio=" + dataI;
                today = "";
            }

            if (dataFine != "" && dataFine != null)
            {
                dataF = DateTime.Parse(dataFine);
                append += "&dataFine=" + dataF;
                today = "";
            }

            if (importoMin != null && importoMin != "")
                append += "&importoMin=" + importoMin;

            if (importoMax != null && importoMax != "")
                append += "&importoMax=" + importoMax;

            ResponseDto? response = await _actionService.GetInstallmentsAsync(token.sub, true, today, false, null, page, itemsPerPage, append);

            if (response is not null && response.IsSuccess)
                _cache.Set("richiesteEsitate", JsonConvert.DeserializeObject<List<GetInstallmentsResult>>(Convert.ToString(response.Result)!));
            else
                _logger.Error(response?.Message);

            return JsonConvert.SerializeObject(response);
        }

        public async Task<string> GetRichiesteInAttesa(int page = 1, int itemsPerPage = 100, string codiceFiscale = "", string iuv = "", string dataInizio = "", string dataFine = "")
        {
            Globals g = new(_tokenProvider);
            TokenDto token;
            token = g.GetDeserializedToken();
            string today = DateTime.Today.ToString();
            string append = "";
            var dataI = new DateTime();
            var dataF = new DateTime();

            if (codiceFiscale != "" && codiceFiscale != null)
                append += "&codiceFiscale=" + codiceFiscale;

            if (iuv != "" && iuv != null)
                append += "&iuv=" + iuv;

            if (dataInizio != "" && dataInizio != null)
            {
                dataI = DateTime.Parse(dataInizio);
                append += "&dataInizio=" + dataI;
                today = "";
            }

            if (dataFine != "" && dataFine != null)
            {
                dataF = DateTime.Parse(dataFine);
                append += "&dataFine=" + dataF;
                today = "";
            }

            ResponseDto? response = await _actionService.GetInstallmentsAsync(token.sub, false, today, false, false, page, itemsPerPage, append);

            if (response is not null && response.IsSuccess)
                _cache.Set("richiesteInAttesa", JsonConvert.DeserializeObject<List<GetInstallmentsResult>>(Convert.ToString(response.Result)!)!);
            else
                _logger.Error(response?.Message);

            return JsonConvert.SerializeObject(response);
        }

        public async Task<string> GetStoricoOperazioni(int page = 1, int itemsPerPage = 100, string dataInizio = "", string dataFine = "")
        {
            Globals g = new(_tokenProvider);
            TokenDto token;
            token = g.GetDeserializedToken();
            string today = DateTime.Today.ToString();
            string append = "";
            var dataI = new DateTime();
            var dataF = new DateTime();

            if (dataInizio != "" && dataInizio != null)
            {
                dataI = DateTime.Parse(dataInizio);
                append += "&dataInizio=" + dataI;
                today = "";
            }

            if (dataFine != "" && dataFine != null)
            {
                dataF = DateTime.Parse(dataFine);
                append += "&dataFine=" + dataF;
                today = "";
            }

            ResponseDto? response = await _actionService.GetOperationsByUserId(token.sub, today, page, itemsPerPage, append);

            if (response is not null && response.IsSuccess)
                _cache.Set("richiesteStoricoOperazioni", JsonConvert.DeserializeObject<List<OperationHistoryViewModel>>(Convert.ToString(response.Result)!)!);
            else
                _logger.Error(response?.Message);

            return JsonConvert.SerializeObject(response);
        }

        [HttpGet]
        public string EliminaFiltroStoricoOperazioni()
        {
            IEnumerable<OperationHistoryViewModel> oHViewModels = (IEnumerable<OperationHistoryViewModel>)_cache.Get("richiesteStoricoOperazioni")!;

            return JsonConvert.SerializeObject(oHViewModels);
        }

        public async Task<string> GetOperationTypes()
        {
            Globals g = new(_tokenProvider);
            TokenDto token = g.GetDeserializedToken();
            List<OperationTypeDto> l = new();
            string today = DateTime.Today.ToString();

            ResponseDto? response = await _actionService.GetOperationTypes();

            if (response is not null && response.IsSuccess)
                l = JsonConvert.DeserializeObject<List<OperationTypeDto>>(Convert.ToString(response.Result)!)!;
            else
                _logger.Error(response?.Message);


            return JsonConvert.SerializeObject(response);
        }

        //public async Task<string> GetOperationTypesBySenderUserId(int senderUserId)
        //{
        //    Globals g = new(_tokenProvider);
        //    TokenDto token = g.GetDeserializedToken();
        //    List<OperationTypesSenderUserDto> l = new();
        //    string today = DateTime.Today.ToString();

        //    ResponseDto? response = await _actionService.GetOperationTypesSenderUsers(senderUserId);

        //    if (response is not null && response.IsSuccess)
        //        l = JsonConvert.DeserializeObject<List<OperationTypesSenderUserDto>>(Convert.ToString(response.Result)!)!;
                
        //    else
        //        _logger.Error(response?.Message);


        //    return JsonConvert.SerializeObject(response);
        //}

        [HttpPost]
        public async Task<string> FiltraRichieste(IFormCollection fc)
        {
            Globals g = new(_tokenProvider);
            TokenDto token;
            token = g.GetDeserializedToken();
            string today = DateTime.Today.ToString();

            string append = "";

            var response = new ResponseDto();

            string iuv = fc["iuv"]!;
            string codiceFiscale = fc["codiceFiscale"]!;
            string dataInizio = fc["dataInizio"]!;
            string dataFine = fc["dataFine"]!;
            string nominativo = fc["nominativo"]!;
            var dataI = new DateTime();
            var dataF = new DateTime();
            bool worked = Convert.ToBoolean(fc["worked"]);
            bool? valid;
            bool? paid = null;


            if (!string.IsNullOrEmpty(fc["valid"]))
                valid = Convert.ToBoolean(fc["valid"]);
            else
                valid = null;


            if (fc["paid"].ToString() != "" && fc["paid"].ToString() != null)
            {
                if (fc["paid"]! == "SI" || fc["paid"]! == "true")
                    paid = true;
                else
                    paid = false;
            }

            if (dataInizio != "" && dataInizio != null)
            {
                dataI = DateTime.Parse(dataInizio);
                append += "&dataInizio=" + dataI;
                today = "";
            }

            if (dataFine != "" && dataFine != null)
            {
                dataF = DateTime.Parse(dataFine);
                append += "&dataFine=" + dataF;
                today = "";
            }

            if (nominativo != null && nominativo != "")
                append += "&nominativo=" + nominativo;

            if (iuv != null && iuv != "")
                append += "&iuv=" + iuv;

            if (codiceFiscale != null && codiceFiscale != "")
                append += "&codiceFiscale=" + codiceFiscale;


            if (fc["payable"].ToString() != "" && fc["payable"].ToString() != null)
            {
                append += "&payable=" + fc["payable"];
            }

            if (fc["importoMin"].ToString() != "" && fc["importoMin"].ToString() != null)
            {
                append += "&importoMin=" + fc["importoMin"];
            }

            if (fc["importoMax"].ToString() != "" && fc["importoMax"].ToString() != null)
            {
                append += "&importoMax=" + fc["importoMax"];
            }

            response = await _actionService.GetInstallmentsAsync(token.sub, worked, today, paid, valid, Convert.ToInt32(fc["page"]), Convert.ToInt32(fc["itemsPerPage"]), append);

            return JsonConvert.SerializeObject(response);
        }

        public async Task<string> FiltraRichiesteStoricoOperazioni(IFormCollection fc)
        {
            Globals g = new(_tokenProvider);
            TokenDto token;
            token = g.GetDeserializedToken();
            string today = DateTime.Today.ToString();

            string append = "";

            var response = new ResponseDto();

            string dataInizio = fc["dataInizio"]!;
            string dataFine = fc["dataFine"]!;
            var dataI = new DateTime();
            var dataF = new DateTime();

            if (dataInizio != "" && dataInizio != null)
            {
                dataI = DateTime.Parse(dataInizio);
                append += "&dataInizio=" + dataI;
                today = "";
            }

            if (dataFine != "" && dataFine != null)
            {
                dataF = DateTime.Parse(dataFine);
                append += "&dataFine=" + dataF;
                today = "";
            }

            response = await _actionService.GetOperationsByUserId(token.sub, today, Convert.ToInt32(fc["page"]), Convert.ToInt32(fc["itemsPerPage"]), append);

            return JsonConvert.SerializeObject(response);
        }


        [HttpGet]
        public string EliminaFiltroRendicontazione()
        {
            IEnumerable<GetRendicontazioniResult> getRendicontazioniResults = (IEnumerable<GetRendicontazioniResult>)_cache.Get("rendicontazionePagamenti")!;

            return JsonConvert.SerializeObject(getRendicontazioniResults);
        }


        [HttpGet]
        public string EliminaFiltroRichiesteEsitate()
        {
            IEnumerable<GetInstallmentsResult> debtPositionInstallmentViewModels = (IEnumerable<GetInstallmentsResult>)_cache.Get("richiesteEsitate")!;

            return JsonConvert.SerializeObject(debtPositionInstallmentViewModels);
        }

        [HttpGet]
        public string EliminaFiltroInAttesa()
        {
            IEnumerable<GetInstallmentsResult> debtPositionInstallmentViewModels = (IEnumerable<GetInstallmentsResult>)_cache.Get("richiesteInAttesa")!;

            return JsonConvert.SerializeObject(debtPositionInstallmentViewModels);
        }


        public List<CsvDtoOut> ConvertCsv(string pathFile, bool bollettino)
        {
            string webRootPath = _webHostEnvironment.WebRootPath;
            string csvFilePath = webRootPath + pathFile;
            List<CsvDto> records = new();
            List<CsvDtoOut> recordsOut = new();
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture);
            csvConfig.Delimiter = ";";
            var reader = new StreamReader(csvFilePath);
            var csv = new CsvReader(reader, csvConfig);
            try
            {
                records = csv.GetRecords<CsvDto>().ToList();
                recordsOut = CheckRecords(records, bollettino);
            }
            catch (Exception ex)
            {
                _logger.Error("  An error occurred in the ConvertCsv() in Action Controller  ", ex);
            }

            return recordsOut;
        }


        public static void WriteCSVStoricoOperazioni(List<StoricoOperazioniViewModel> items, string filename)
        {
            var csv = "Titolo;Rata;IUV;Tipologia;Data Scadenza;Importo;Anagrafica;Codice Fiscale;CAP;Comune;Provincia;Indirizzo\n";
            foreach (var item in items)
            {
                csv +=
                    item.title + ";";

                //per far visualizzare la descrizione delle multe e non 'Rata unica' senza poter distinguere se è il pagamento entro o dopo 5 giorni
                if (item.description!.Contains("rata"))
                    csv += (item.numeroRata == 0 ? "Rata unica" : item.numeroRata.ToString()) + ";";
                else
                    csv += item.description + ";";

                csv += item.iuv + ";" +
                GetOperationTypeString(item.operationTypeId) + ";" +
                ' ' + item.expirationDate.ToString("dd/MM/yyyy") + ";" +
                string.Format("{0:0.00} €", item.price) + ";" +
                item.anagraficaPagatore + ";" +
                item.codiceIdentificativoUnivocoPagatore + ";" +
                item.capPagatore!.PadLeft(5, '0') + ";" + 
                item.comunePagatore + ";" +
                item.provinciaPagatore + ";" +
                item.indirizzoPagatore + ";" + "\n";
            }
            System.IO.File.WriteAllText(filename, csv.ToString(), Encoding.UTF8);
        }


        public static void WriteCSVRendicontazione(List<GetRendicontazioniResult> items, string filename)
        {
            var csv = "Nominativo;Codice Fiscale;Tipologia;IUV;Importo;Rata;Data Scadenza;Pagato\n";
            foreach (var item in items)
            {
                csv +=
                    item.anagraficaPagatore + ";" +
                    item.codiceFiscale + ";" +
                    item.tipoOperazione + ";" +
                    item.iuv + ";" +
                    string.Format("{0:0.00} €", item.prezzo) + ";" +
                    (item.numeroRata == 0 ? "Rata unica" : item.numeroRata.ToString()) + ";" +
                    ' ' + item.dataScadenza.ToString("dd/MM/yyyy") + ";" +
                    (item.pagato == true ? "SI" : "NO") + "\n";
            }
            System.IO.File.WriteAllText(filename, csv.ToString(), Encoding.UTF8);
        }


        public static void WriteCSVRichiesteEsitate(List<GetInstallmentsResult> items, string filename)
        {
            var csv = "Iuv;Codice Fiscale;Tipologia;Importo;Rata;Data Scadenza;Stato\n";
            foreach (var item in items)
            {
                csv +=
                    item.iuv + ";" +
                    item.codiceIdentificativoUnivocoPagatore + ";" +
                    GetOperationTypeString(item.operationTypeId) + ";" +
                    string.Format("{0:0.00} €", item.price) + ";" +
                    (item.numeroRata == 0 ? "Rata unica" : item.numeroRata.ToString()) + ";" +
                    ' ' + item.expirationDate.ToString("dd/MM/yyyy") + ";" +
                    (item.valid == true ? "ESITATA" : "NON VALIDATA") + "\n";
            }
            System.IO.File.WriteAllText(filename, csv.ToString(), Encoding.UTF8);
        }

        private static string GetOperationTypeString(int operationTypeId)
        {
            switch (operationTypeId)
            {
                case 1:
                    return "Tari anni precedenti";
                case 2:
                    return "Mensa scolastica";
                case 3:
                    return "Multa";
                case 4:
                    return "Canone unico";
                case 5:
                    return "Passo carrabile";
                case 6:
                    return "Trasporto scolastico";
                case 7:
                    return "Diritti di segreteria per certificati anagrafici";
                case 8:
                    return "Affitti";
                case 9:
                    return "Tassa concorso";
                case 10:
                    return "Diritti di segreteria e spese di notifica";
                case 11:
                    return "Aree Mercatali";
                case 12:
                    return "COSAP/TOSAP";
                case 13:
                    return "Tari anno in corso";
                case 14:
                    return "Servizio acqua, luce e gas";
                default:
                    return "";
            }
        }


        public string? CreateZipFile(List<Dictionary<string, string>> l)
        {
            try
            {
                var nf = DateTime.Now.Ticks;
                string webRootPath = _webHostEnvironment.WebRootPath;
                var newPath = webRootPath + Globals.FolderDownloadZip + nf;
                if (!Directory.Exists(newPath))
                    Directory.CreateDirectory(newPath);

                var files = new List<string>();

                foreach (var i in l)
                {
                    var fileName = i.First().Key.Split("/").Last();
                    var base64 = i.First().Value;
                    Globals.ConvertBase64ToFile(newPath + "/" + fileName, base64);
                    files.Add(newPath + "/" + fileName);
                }

                var zipFile = newPath + "/" + nf + ".zip";
                using (var archive = ZipFile.Open(zipFile, ZipArchiveMode.Create))
                {
                    foreach (var fPath in files)
                        archive.CreateEntryFromFile(fPath, Path.GetFileName(fPath));
                }
                return Globals.FolderDownloadZip + nf + "/" + nf + ".zip";
            }
            catch (Exception e)
            {
                _logger.Error("  An error occurred in the CreateZipFile in Action Controller  " + e.Message);
            }

            return null;
        }


        public List<string> OpenZipFile(string pathFile)
        {
            var l = new List<string>();
            string webRootPath = _webHostEnvironment.WebRootPath;
            try
            {
                var nf = DateTime.Now.Ticks;
                var newPath = webRootPath + Globals.FolderZip + nf;
                if (!Directory.Exists(newPath))
                    Directory.CreateDirectory(newPath);

                ZipFile.ExtractToDirectory(webRootPath + pathFile, newPath);

                string[] files = Directory.GetFiles(newPath);
                foreach (string file in files)
                    l.Add(Globals.FolderZip + nf + "/" + Path.GetFileName(file));
            }
            catch (Exception)
            {
                _logger.Error("  An error occurred in the OpenZipFile in Action Controller  ");
            }

            return l;
        }


        private bool CheckDate(string date)
        {
            try
            {
                var split = date.Split('-');
                if (split.Length < 3)
                    return false;

                var year = split[0];
                var month = split[1];
                var day = split[2];

                if (year.Length < 4)
                    return false;

                if (Convert.ToInt32(year) < 1920)
                    return false;

                if (month.Length < 2)
                    return false;

                if (Convert.ToInt32(month) > 12)
                    return false;

                if (day.Length < 2)
                    return false;

                if (Convert.ToInt32(day) > 31)
                    return false;


                return true;
            }
            catch (Exception)
            {

            }
            return false;
        }


        public List<CsvDtoOut> CheckRecords(List<CsvDto> records, bool bollettino)
        {
            var resRecord = new List<CsvDtoOut>();
            foreach (var r in records)
            {
                var nr = new CsvDtoOut()
                {
                    codicefiscale = r.codicefiscale,
                    totale = r.totale,
                    numeroRatei = r.numeroRatei,
                    nominativo = r.nominativo,
                    dataScadenzaRata1 = r.dataScadenzaRata1,
                    dataScadenzaRata10 = r.dataScadenzaRata10,
                    dataScadenzaRata11 = r.dataScadenzaRata11,
                    dataScadenzaRata12 = r.dataScadenzaRata12,
                    dataScadenzaRata2 = r.dataScadenzaRata2,
                    dataScadenzaRata3 = r.dataScadenzaRata3,
                    dataScadenzaRata4 = r.dataScadenzaRata4,
                    dataScadenzaRata5 = r.dataScadenzaRata5,
                    dataScadenzaRata6 = r.dataScadenzaRata6,
                    dataScadenzaRata7 = r.dataScadenzaRata7,
                    dataScadenzaRata8 = r.dataScadenzaRata8,
                    dataScadenzaRata9 = r.dataScadenzaRata9,
                    dataScadenzaRataUnica = r.dataScadenzaRataUnica,
                    nomeFile = r.nomeFile,
                    cap = r.cap,
                    indirizzo = r.indirizzo,
                    comune = r.comune,
                    provincia = r.provincia
                };
                try
                {
                    var n = Convert.ToInt32(r.numeroRatei);
                    switch (n)
                    {
                        case 0:
                            nr.valid = true;
                            break;
                        case 1:
                            try
                            {
                                if (CheckDate(r.dataScadenzaRata1!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata1);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }
                            }
                            catch
                            {
                                nr.message = "Data scadenza non corretta o non presente";
                            }
                            break;
                        case 2:
                            try
                            {
                                if (CheckDate(r.dataScadenzaRata1!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata1);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata2!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata2);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }
                            }
                            catch
                            {
                                nr.message = "Data scadenze ratei non corrette o non presenti";
                            }
                            break;
                        case 3:
                            try
                            {
                                if (CheckDate(r.dataScadenzaRata1!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata1);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata2!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata2);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata3!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata3);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }


                            }
                            catch
                            {
                                nr.message = "Data scadenze ratei non corrette o non presenti";
                            }
                            break;
                        case 4:
                            try
                            {
                                if (CheckDate(r.dataScadenzaRata1!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata1);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata2!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata2);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata3!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata3);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata4!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata4);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                            }
                            catch
                            {
                                nr.message = "Data scadenze ratei non corrette o non presenti";
                            }
                            break;
                        case 5:
                            try
                            {
                                if (CheckDate(r.dataScadenzaRata1!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata1);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata2!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata2);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata3!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata3);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata4!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata4);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata5!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata5);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }
                            }
                            catch
                            {
                                nr.message = "Data scadenze ratei non corrette o non presenti";
                            }
                            break;
                        case 6:
                            try
                            {
                                if (CheckDate(r.dataScadenzaRata1!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata1);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata2!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata2);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata3!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata3);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata4!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata4);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata5!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata5);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata6!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata6);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }
                            }
                            catch
                            {
                                nr.message = "Data scadenze ratei non corrette o non presenti";
                            }
                            break;
                        case 7:
                            try
                            {
                                if (CheckDate(r.dataScadenzaRata1!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata1);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata2!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata2);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata3!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata3);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata4!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata4);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata5!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata5);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata6!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata6);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata7!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata7);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }
                            }
                            catch
                            {
                                nr.message = "Data scadenze ratei non corrette o non presenti";
                            }
                            break;
                        case 8:
                            try
                            {
                                if (CheckDate(r.dataScadenzaRata1!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata1);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata2!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata2);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata3!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata3);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata4!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata4);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata5!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata5);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata6!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata6);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata7!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata7);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata8!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata8);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }
                            }
                            catch
                            {
                                nr.message = "Data scadenze ratei non corrette o non presenti";
                            }
                            break;
                        case 9:
                            try
                            {
                                if (CheckDate(r.dataScadenzaRata1!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata1);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata2!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata2);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata3!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata3);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata4!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata4);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata5!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata5);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata6!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata6);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata7!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata7);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata8!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata8);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata9!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata9);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }
                            }
                            catch
                            {
                                nr.message = "Data scadenze ratei non corrette o non presenti";
                            }
                            break;
                        case 10:
                            try
                            {
                                if (CheckDate(r.dataScadenzaRata1!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata1);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata2!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata2);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata3!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata3);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata4!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata4);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata5!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata5);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata6!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata6);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata7!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata7);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata8!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata8);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata9!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata9);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata10!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata10);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }
                            }
                            catch
                            {
                                nr.message = "Data scadenze ratei non corrette o non presenti";
                            }
                            break;
                        case 11:
                            try
                            {
                                if (CheckDate(r.dataScadenzaRata1!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata1);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata2!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata2);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata3!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata3);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata4!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata4);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata5!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata5);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata6!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata6);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata7!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata7);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata8!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata8);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata9!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata9);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata10!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata10);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata11!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata11);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }
                            }
                            catch
                            {
                                nr.message = "Data scadenze ratei non corrette o non presenti";
                            }
                            break;
                        case 12:
                            try
                            {
                                if (CheckDate(r.dataScadenzaRata1!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata1);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata2!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata2);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata3!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata3);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata4!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata4);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata5!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata5);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata6!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata6);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata7!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata7);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata8!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata8);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata9!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata9);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata10!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata10);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata11!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata11);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }

                                if (CheckDate(r.dataScadenzaRata12!))
                                {
                                    var d = Convert.ToDateTime(r.dataScadenzaRata12);
                                    nr.valid = true;
                                }
                                else
                                {
                                    nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                                    nr.valid = false;
                                    break;
                                }
                            }
                            catch
                            {
                                nr.message = "Data scadenze ratei non corrette o non presenti";
                            }
                            break;
                    }
                }
                catch
                {
                    nr.valid = false;
                    nr.message = "il numero ratei deve essere un intero";
                }

                if (!Globals.CheckFc(r.codicefiscale))
                {
                    nr.message = "codice fiscale non valido";
                    nr.valid = false;
                }

                try
                {
                    if (CheckDate(r.dataScadenzaRataUnica!))
                    {
                        var d = Convert.ToDateTime(r.dataScadenzaRataUnica);
                        nr.valid = true;
                    }
                    else
                    {
                        nr.message = "Data scadenza deve essere scritta nel tipo yyyy-MM-dd";
                        nr.valid = false;
                        break;
                    }

                }
                catch
                {
                    nr.message = "data scadenza rata unica non valida";
                    nr.valid = false;
                }

                try
                {
                    Convert.ToDecimal(r.totale);
                }
                catch
                {
                    nr.message = "totale rata unica non valido";
                    nr.valid = false;
                }

                if (bollettino && (r.nomeFile == null || r.nomeFile == ""))
                {
                    nr.message = "nome del file non presente";
                    nr.valid = false;
                }

                resRecord.Add(nr);
            }
            return resRecord;
        }


        public IActionResult RichiesteEsitate()
        {
            return View();
        }


        public ActionResult RendicontazionePagamenti()
        {
            return View();
        }


        public ActionResult StoricoOperazioni()
        {
            return View();
        }


        [HttpPost]
        public async Task<string> GetIuvInCsv(IFormCollection fc)
        {
            Globals g = new(_tokenProvider);
            TokenDto token;
            token = g.GetDeserializedToken();
            string today = DateTime.Today.ToString();

            string append = "";

            string iuv = fc["iuv"]!;
            string codiceFiscale = fc["codiceFiscale"]!;
            string dataInizio = fc["dataInizio"]!;
            string dataFine = fc["dataFine"]!;
            string nominativo = fc["nominativo"]!;
            string importoMin = fc["importoMin"]!;
            string importoMax = fc["importoMax"]!;
            var dataI = new DateTime();
            var dataF = new DateTime();
            bool worked = Convert.ToBoolean(fc["worked"]);

            bool? valid;
            if (!string.IsNullOrEmpty(fc["valid"]))
                valid = Convert.ToBoolean(fc["valid"]);
            else
                valid = null;

            bool? paid = null;

            if (fc["paid"].ToString() != "" && fc["paid"].ToString() != null)
            {
                if (fc["paid"]! == "SI")
                    paid = true;
                else
                    paid = false;
            }

            if (fc["paid"].ToString() == "" || fc["paid"].ToString() == null)
                paid = false;

            if (dataInizio != "" && dataInizio != null)
            {
                dataI = DateTime.Parse(dataInizio);
                append += "&dataInizio=" + dataI;
                today = "";
            }

            if (dataFine != "" && dataFine != null)
            {
                dataF = DateTime.Parse(dataFine);
                append += "&dataFine=" + dataF;
                today = "";
            }

            if (nominativo != null && nominativo != "")
                append += "&nominativo=" + nominativo;

            if (iuv != null && iuv != "")
                append += "&iuv=" + iuv;

            if (codiceFiscale != null && codiceFiscale != "")
                append += "&codiceFiscale=" + codiceFiscale;

            if (importoMin != null && importoMin != "")
                append += "&importoMin=" + importoMin;

            if (importoMax != null && importoMax != "")
                append += "&importoMax=" + importoMax;

            string webRootPath = _webHostEnvironment.WebRootPath;
            string fileName = Globals.FolderDownloadCsv + DateTime.Now.Ticks + ".csv";

            switch (fc["type"].ToString().ToUpper())
            {
                case "RICHIESTEESITATE":
                    var response = await _actionService.GetInstallmentsAsync(token.sub, worked, today, paid, valid, Convert.ToInt32(fc["page"]), Convert.ToInt32(fc["itemsPerPage"]), append);
                    var l = JsonConvert.DeserializeObject<List<GetInstallmentsResult>>(response.Result!.ToString()!);
                    WriteCSVRichiesteEsitate(l!, webRootPath + fileName);
                    break;
            }
            return fileName;
        }


        [HttpPost]
        public async Task<string> GeneraRendicontazione(IFormCollection fc)
        {
            var l = new List<GetRendicontazioniResult>();
            Globals g = new(_tokenProvider);
            TokenDto token;
            token = g.GetDeserializedToken();
            string today = DateTime.Today.ToString();

            string append = "";

            string iuv = fc["iuv"]!;
            string codiceFiscale = fc["codiceFiscale"]!;
            string dataInizio = fc["dataInizio"]!;
            string dataFine = fc["dataFine"]!;
            string nominativo = fc["nominativo"]!;
            string importoMin = fc["importoMin"]!;
            string importoMax = fc["importoMax"]!;
            var dataI = new DateTime();
            var dataF = new DateTime();

            bool? paid = null;
            bool? payable = null;

            if (fc["paid"].ToString() != "")
            {
                if (fc["paid"]! == "SI")
                {
                    paid = true;
                    payable = false;
                }
                else
                {
                    paid = false;
                    payable = true;
                }
            }

            if (dataInizio != "" && dataInizio != null)
            {
                dataI = DateTime.Parse(dataInizio);
                append += "&dataInizio=" + dataI;
                today = "";
            }

            if (dataFine != "" && dataFine != null)
            {
                dataF = DateTime.Parse(dataFine);
                append += "&dataFine=" + dataF;
                today = "";
            }

            if (nominativo != null && nominativo != "")
                append += "&nominativo=" + nominativo;

            if (iuv != null && iuv != "")
                append += "&iuv=" + iuv;

            if (codiceFiscale != null && codiceFiscale != "")
                append += "&codiceFiscale=" + codiceFiscale;

            if (importoMin != null && importoMin != "")
                append += "&importoMin=" + importoMin;

            if (importoMax != null && importoMax != "")
                append += "&importoMax=" + importoMax;

            append += "&payable=" + payable;

            string webRootPath = _webHostEnvironment.WebRootPath;
            string fileName = Globals.FolderDownloadCsv + DateTime.Now.Ticks + ".csv";

            var response = await _actionService.GetRendicontazione(token.sub, today, paid, append);
            l = JsonConvert.DeserializeObject<List<GetRendicontazioniResult>>(response.Result!.ToString()!);
            WriteCSVRendicontazione(l!, webRootPath + fileName);

            return fileName;
        }

        [HttpGet]
        public async Task<string> GetCsvFromOperation(int id)
        {
            string webRootPath = _webHostEnvironment.WebRootPath;
            string fileName = Globals.FolderDownloadCsv + DateTime.Now.Ticks + ".csv";
            var l = new List<StoricoOperazioniViewModel>();
            ResponseDto? response = await _actionService.GetIUVFromOperation(id);
            l = JsonConvert.DeserializeObject<List<StoricoOperazioniViewModel>>(response.Result!.ToString()!);
            WriteCSVStoricoOperazioni(l!, webRootPath + fileName);
            return fileName;
        }

        [HttpGet]
        public async Task<string> GetFileFromOperation(int id)
        {
            string webRootPath = _webHostEnvironment.WebRootPath;
            var l = new List<Dictionary<string, string>>();
            ResponseDto? response = await _actionService.GetFilesFromOperation(id);
            l = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(response.Result!.ToString()!);
            return CreateZipFile(l!)!;
        }



    }
}