using CodeCase.Models;
using DBConnection;
using DBConnection.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MongoDB.Driver;

namespace CodeCase.Controllers
{
    public class WorkController : Controller
    {
        MongoDbContext _context;

        public WorkController(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var a = await _context.GetConfigDataCollection().FindAsync(x => x.IsActive == 1);
            return View(await a.ToListAsync());
        }


        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Type,Value")] cs_ConfigData cs_ConfigData)
        {
            if (ModelState.IsValid)
            {
                var collection = _context.GetConfigDataCollection();
                int id = await _context.GetCollectionId(MongoDbContext.collectionName);
                cs_ConfigData.Id = id;

                collection.InsertOne(cs_ConfigData);

                return RedirectToAction(nameof(Index));
            }
            return View(cs_ConfigData);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || id < 0)
            {
                return RedirectToAction(nameof(Index));
            }

            var data = await _context.GetConfigDataCollection()
                .Find(x => x.IsActive == 1 && x.Id == id).FirstOrDefaultAsync();

            if (data == null)
            {
                return RedirectToAction(nameof(Index));
            }

            return View(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int? id, [Bind("Id,Name,Type,Value")] cs_ConfigData cs_ConfigData)
        {
            if (id != cs_ConfigData.Id)
            {
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                var filter = Builders<cs_ConfigData>.Filter.Eq(x => x.Id, cs_ConfigData.Id);
                var update = Builders<cs_ConfigData>.Update
                    .Set(x => x.Name, cs_ConfigData.Name)
                    .Set(x => x.Type, cs_ConfigData.Type)
                    .Set(x => x.Value, cs_ConfigData.Value);

                await _context.GetConfigDataCollection()
                              .UpdateOneAsync(filter, update);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int? id)
        {

            var filter = Builders<cs_ConfigData>.Filter.Eq(x => x.Id, id);
            var update = Builders<cs_ConfigData>.Update.Set(x => x.IsActive, 0);

            await _context.GetConfigDataCollection()
                          .UpdateOneAsync(filter, update);

            return RedirectToAction(nameof(Index));
        }

    }
}
