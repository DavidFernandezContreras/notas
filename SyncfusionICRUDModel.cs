        [Route("[action]")]
        public ActionResult CrudUpdate([FromBody] ICRUDModel<TestCase> testCase)
        {
            ICRUDModel<TestCase> value = testCase;

            if (value != null && value.Action == "update")
            {
                TestCase ord = value.Value;
                if (Update(ord.Id, ord, _urls.HttpTestCases))
                {
                    return Ok(value.Value);
                }
                else
                {
                    _logger.LogError("Error updating object.Id Object[" + Convert.ToString(value.Key) + "]");
                    return StatusCode(500, "Error updating object.Id Object[" + Convert.ToString(value.Key) + "]");
                }
            }
            else if (value != null && value.Action == "insert")
            {
                Insert(value.Value, _urls.HttpTestCases);
                return Ok(value.Value);
            }
            else if (value != null && value.Key != null && value.Action == "remove")
            {
                RequestResult requestResult = base.TryDelete(Convert.ToString(value.Key)!, _urls.HttpTestCases);

                FrontEndNotification frontEndNotification = _mapper.Map<RequestResult, FrontEndNotification>(requestResult);

                return Ok(frontEndNotification);
            }
            else
            {
                return BadRequest();
            }
        }

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GTTestManagmentTool.Utils
{
    public class ICRUDModel<T> where T : class
    {
        public string Action { get; set; }

        public string Table { get; set; }

        public string KeyColumn { get; set; }

        public object Key { get; set; }

        public T Value { get; set; }

        public List<T> Added { get; set; }

        public List<T> Changed { get; set; }

        public List<T> Deleted { get; set; }

        public IDictionary<string, object> Params { get; set; }
    }
}
