// $Id: hash.mqh 125 2014-03-03 08:38:32Z ydrol $
#ifndef YDROL_HASH_MQH
#define YDROL_HASH_MQH

//#property strict

/*
   This is losely ported from a C version I have which was in turn modified from hashtable.c by Christopher Clark.
 Copyright (C) 2014, Andrew Lord (NICKNAME=lordy) <forex@NICKNAME.org.uk> 
 Copyright (C) 2002, 2004 Christopher Clark <firstname.lastname@cl.cam.ac.uk> 

 2014/02/21 - Readded PrimeNumber sizes and auto rehashing when load factor hit.
*/

      

/// Any value stored in a Hash must be a subclass of HashValue
class HashValue {
};

/// Linked list of values - there will be one list for each hash value
class HashEntry {
    public:
        string _key;
        HashValue * _val;
        HashEntry *_next;

        HashEntry() {
            _key=NULL;
            _val=NULL;
            _next=NULL;
        }

        HashEntry(string key,HashValue* val) {
            _key=key;
            _val=val;
            _next=NULL;
        }

        ~HashEntry() {
        }
};

/// Convenience class for storing strings as hash values.
class HashString : public HashValue {
    private:
        string val;
    public:
        HashString(string v) { val=v;}
        string getVal() { return val; }
};

/// Convenience class for storing doubles as hash values.
class HashDouble : public HashValue {
    private:
        double val;
    public:
        HashDouble(double v) { val=v;}
        double getVal() { return val; }
};

/// Convenience class for storing ints as hash values.
class HashInt : public HashValue {
    private:
        int val;
    public:
        HashInt(int v) { val=v;}
        int getVal() { return val; }
};

/// Convenience class for storing longs as hash values.
class HashLong : public HashValue {
    private:
        long val;
    public:
        HashLong(datetime v) { val=v;}
        long getVal() { return val; }

};

/// Convenience class for storing datetimes as hash values.
class HashDatetime : public HashValue {
    private:
        datetime val;
    public:
        HashDatetime(datetime v) { val=v;}
        datetime getVal() { return val; }
};

///
/// Hash class allows objects to be stored in a table index by strings.
/// the stored Objects must be a sub class of the HashValue class.
///
/// There are some convenience classes to hold atomic types as values HashString,HashDouble,HashInt
///
///EXAMPLE:
///
/// <pre>
/// class myClass: public HashValue {
///   public: int v;
///   myClass(int a) { v = a;}
/// };
///
/// // Create the objects as needed
///
///      myClass *a = new myClass(1);
///      myClass *b = new myClass(2);
///      myClass *c = new myClass(3);
///
/// // Then to insert into hash etc.
///
///      Hash* h = new Hash(193,true); 
///      // 'true' means when the hash will adopt the values and delete them when they are removed from the hash or when the hash is deleted.
///
///      h.hPut("a",a);
///      h.hPut("b",b);
///      h.hPut("c",c);
///
///      myClass *d = h.hGet("b");
///
///      etc.
///
/// // Iterate over hash
///    HashLoop *l
///    for (l = new HashLoop(h) ; l.hasNext() ; l.next()  ) {
///        string key = l.key();
///        MyClass *c = l.val();
///    }
///    delete l;
///
///    // Delete from hash - This will also delete 'a' because we set the 'adopt' flag on the hash.
///    h.hDel("a");
///
///    //Delete the hash - this will also delete 'b' and 'c' because of the adopt flag.
///    delete h;
/// </pre> 
class Hash : public HashValue {

private:
    /// Number of slots in the hashtable. 
    /// this should be approx number of elements to store. Depending on hash algorithm
    /// it may optimally be a prime or a power of two etc. but probably not important
    /// for MQL4 performance. A future optimisation might be to move the hashcode function to a DLL??
    uint _hashSlots; 

    /// Number of elements at which hash will get resized.
    int _resizeThreshold;

    /// number of things in the hash
    int _hashEntryCount;

    /// an array of linked lists (HashEntry). one for each hash value.
    /// To store an object against a string(key) - get the string hashcode, then insert pair (key,val) into the linked list for that hashcode.
    /// To fetch an object against a string(key) - get the string hashcode, get linked-list at that hashcode index, then search for the key and return the val.
    HashEntry* _buckets[];

    /// If true the hash will free(delete) values as they are removed, or at cleanup.
    bool _adoptValues;

    int _errCode;
    string _errText;

    void init(uint size,bool adoptValues)
    {
        _hashSlots = 0;
        _hashEntryCount = 0;
        clearError();
        setAdoptValues(adoptValues);

        rehash(size);
    }

    // Hash table distribution is better when size is prime, eg if hash function procduces numbers
    // that are multiples of x, then there may be grouping occuring around gcd(x,slots) gcd(2x,slots) etc
    // using a prime size helps spread the distribution.
    uint size2prime(uint size) {
        int pmax=ArraySize(_primes);
        for(int p=0 ; p<pmax; p++ ) {
            if (_primes[p] >= size) {
               return _primes[p];
            }
        }
        return size; 
    }

    /// Primes that approx double in size, used for hash table sizes to avoid gcd causing bunching
    static uint _primes[];

    /// After reviewing quite a few hash functions I settled on the one below.
    /// http://www.cse.yorku.ca/~oz/hash.html
    /// this is the bottleneck function. Shame mql hash no default hash method for objects.
    uint hash(string s)
    {

        uchar c[];
        uint h = 0;

        if (s != NULL) {
            h = 5381;
            int n = StringToCharArray(s,c);
            for(int i = 0 ; i < n ; i++ ) {
                h = ((h << 5 ) + h ) + c[i];
            }
        }
        return h % _hashSlots;
    }
    void clearError() {
        setError(0,"");
    }
    void setError(int e,string m) {
        _errCode = e;
        _errText = m;
        //error((string)e,m);
    }

public:

    /// Constructor: Create a Hash Object
    Hash() {
        init(17,true);
    }


    /// Constructor: Create a Hash Object
    /// @param adoptValues : If true the hash destructor will <b>delete</b> all dynamically allocated hash values.
    Hash(bool adoptValues) {
        init(17,adoptValues);
    }

    /// Constructor: Create a Hash Object
    /// @param size : Approximate size (actual size will be a larger prime number close to a power of 2)
    /// @param adoptValues : If true the hash destructor will <b>delete</b> all dynamically allocated hash values.
    Hash(int size,bool adoptValues) {
        init(size,adoptValues);
    }

    ~Hash() {

        // Free entries.
        for(uint i = 0 ; i< _hashSlots ; i++) {
            HashEntry *nextEntry = NULL;
            for(HashEntry *entry = _buckets[i] ; entry!= NULL ; entry = nextEntry ) 
            {
                nextEntry = entry._next;

                if (_adoptValues && entry._val != NULL && CheckPointer(entry._val) == POINTER_DYNAMIC ) {
                    delete entry._val;
                }
                delete entry;
            }
            _buckets[i] = NULL;
        }
    }

    /// Return any error that has occured. This should be used when
    /// retriving values in a Hash that may contain NULLs. hGet()
    /// methods can return NULL if not found, in which case getErrorCode
    /// will be set.
    int getErrCode() {
        return _errCode;
    }
    /// Return text of the error message.
    string getErrText() {
        return _errText;
    }

    /// If true the hash destructor will <b>delete</b> all dynamically allocated hash values.
    void setAdoptValues(bool v) {
        _adoptValues = v;
    }

    /// True if the hash destructor will <b>delete</b> all dynamically allocated hash values.
    bool getAdoptValues() {
        return _adoptValues;
    }

    private:
    uint _foundIndex;       // After find() is called is set to hashindex for name whether found or not.
    HashEntry* _foundEntry; // After find() is called  is set to the HashEntry that contains the key.
    HashEntry* _foundPrev;  // After find() is called  is set to the HashEntry before the entry
                            // (could use double linked list but requires more memory).

    /// Look for the required entry for key 'name' true if found.
    bool find(string keyName) {
    
         //Alert("finding");
        bool found = false;

        // Get the index using the hashcode of the string
        _foundIndex = hash(keyName);
        

        if (_foundIndex>_hashSlots ) {

            setError(1,"hGet: bad hashIndex="+(string)_foundIndex+" size "+(string)_hashSlots);

        } else {

            // Search the linked list determined by the index.
            
            for(HashEntry *e = _buckets[_foundIndex] ; e != NULL ; e = e._next )  {
                if (e._key == keyName) {
                    _foundEntry = e;
                    found=true;
                    break;
                }
                // Track the item before the target item in case deleting from single linked list.
                _foundPrev = e;
            }
        }

        return found;
    }

    public:

    /// This is used by the HashLoop class to get start of LinkedList at bucket[i]
    HashEntry*getEntry(int i) {
        return _buckets[i];
    }

    /// Return the number of slots/buckets (not number of elements)
    uint getSlots() {
        return _hashSlots;
    }
    /// Return the number of elements in the Hash
    int getCount() {
        return _hashEntryCount;
    }

    /// Change the hash size and re-allocate values to new buckets.
    bool rehash(uint newSize) {
        bool ret = false;
        HashEntry* oldTable[];

        uint oldSize = _hashSlots;
        newSize  = size2prime(newSize);
        //info("rehashing from "+(string)_hashSlots+" to "+(string)newSize+" "+(string)GetTickCount());

        if (newSize <= getSlots()) {
            setError(2,"rehash "+(string)newSize+" <= "+(string)_hashSlots);
        } else if (ArrayResize(_buckets,newSize) != newSize) {
            setError(3,"unable to resize ");
        } else if (ArrayResize(oldTable,oldSize) != oldSize) {
            setError(4,"unable to resize old copy ");
        } else {
            //Copy old table.
            uint i;
            for(i = 0 ; i < oldSize ; i++ ) oldTable[i] = _buckets[i];
            // Init new entries - not sure if MQL does this anyway
            for(i = 0 ; i<newSize ; i++ ) _buckets[i] = NULL;

            // Move entries to new slots
            _hashSlots = newSize;
            _resizeThreshold = (int)_hashSlots / 4 * 3; // Just use the default load factor value of Javas HashTable

            // Look through all slots
            for(uint oldHashCode = 0 ; oldHashCode<oldSize ; oldHashCode++ ) {
                HashEntry *next = NULL;

                // Walk linked list
                for(HashEntry *e = oldTable[oldHashCode] ; e != NULL ; e = next )  {

                    next = e._next;

                    uint newHashCode = hash(e._key);
                    // Insert at head of new list.
                    e._next = _buckets[newHashCode];
                    _buckets[newHashCode] = e;
                }

                oldTable[oldHashCode] = NULL;
            }
            ret = true;
        }
        return ret;
    }

    /// Check if the hash contains the given key
    /// @param keyName : The key
    /// @return: true if found otherwise false
    bool hContainsKey(string keyName) {
        return find(keyName);
    }

    /// Fetch a value using string key
    ///  @return :HashValue associated with the key (or NULL if none found)
    ///  If the Hashtable contains legitimate NULL values then also check errCode()
    ///  Examples:
    ///   If not storing nulls use
    ///    obj = hash.hGet(x); if (obj != NULL) OK
    ///
    ///  If storing nulls use
    ///     obj = hash.hGet(x); if (obj != NULL || hash.errCode() == 0 ) OK
    HashValue* hGet(string keyName) {

        HashValue *obj = NULL;
        clearError();
        bool found=false;

        if (find(keyName)) {
            obj = _foundEntry._val;
        } else {
            //If Hash contains nulls then also check the errorCode=0 when retrieving
            if (!found) {
                setError(1,"not found");
            }
        }
        return obj;
    }

    /// Convenience method for getting values from a HashString value (see hPutString())
    string hGetString(string keyName) {
        string ret = NULL;
        HashString *v = hGet(keyName);
        if (v != NULL) {
            ret = v.getVal();
        }
        return ret;
    }
    /// Convenience method for getting values from a HashDouble value (see hPutDouble())
    double hGetDouble(string keyName) {
        double ret = NULL;
        HashDouble *v = hGet(keyName);
        if (v != NULL) {
            ret = v.getVal();
        }
        return ret;
    }
    /// Convenience method for getting values from a HashInt value (see hPutInt())
    int hGetInt(string keyName) {
        int ret = NULL;
        HashInt *v = hGet(keyName);
        if (v != NULL) {
            ret = v.getVal();
        }
        return ret;
    }
    /// Convenience method for getting  values from a HashLong ( see hPutLong())
    long hGetLong(string keyName) {
        long ret = NULL;
        HashLong *v = hGet(keyName);
        if (v != NULL) {
            ret = v.getVal();
        }
        return ret;
    }
    /// Convenience method for getting  values from a HashDatetime ( see hPutDatetime())
    datetime hGetDatetime(string keyName) {
        datetime ret = NULL;
        HashDatetime *v = hGet(keyName);
        if (v != NULL) {
            ret = v.getVal();
        }
        return ret;
    }

    /// Store a hash value against the <b>keyName</b> key. This will overwrite any existing
    /// value. It adoptValues is set, it will also free the value if applicable.
    /// @param keyName : key name
    /// @param obj : Value to store
    /// @return the previous value of the key or NULL if there wasnt one 
    HashValue *hPut(string keyName,HashValue *obj) {
    
        HashValue *ret = NULL;
        clearError();
         
        if (find(keyName)) {
            // Return revious value
            ret = _foundEntry._val;
            /*
            // Replace entry contents
            if (_adoptValues && _foundEntry._val != NULL && CheckPointer(_foundEntry._val) == POINTER_DYNAMIC ) {
                delete _foundEntry._val;
            }
            */
            _foundEntry._val = obj;

        } else {
            // Insert new entry at head of list
            HashEntry* e = new HashEntry(keyName,obj);
            HashEntry* first = _buckets[_foundIndex];
            e._next = first;
            _buckets[_foundIndex] = e;
            _hashEntryCount++;

            //info((string)_hashEntryCount+" vs. "+(string)_resizeThreshold);
            // Auto Resize if number of entries hits _resizeThreshold
            if (_hashEntryCount > _resizeThreshold ) {
                rehash(_hashSlots/2*3); // this will snap to the next prime
            }
        }
        return ret;
    }
    /// Store a string as hash value (HashString)
    /// @return the previous value of the key or NULL if there wasnt one 
    HashValue* hPutString(string keyName,string s) {
        HashString *v = new HashString(s);
        return hPut(keyName,v);
    }
    /// Store a double as hash value (HashDouble)
    /// @return the previous value of the key or NULL if there wasnt one 
    HashValue* hPutDouble(string keyName,double d) {
        HashDouble *v = new HashDouble(d);
        return hPut(keyName,v);
    }
    /// Store an int as hash value (HashInt)
    /// @return the previous value of the key or NULL if there wasnt one 
    HashValue* hPutInt(string keyName,int i) {
        HashInt *v = new HashInt(i);
        return hPut(keyName,v);
    }

    /// Store a datetime as hash value (HashLong)
    /// @return the previous value of the key or NULL if there wasnt one 
    HashValue* hPutLong(string keyName,long i) {
        HashLong *v = new HashLong(i);
        return hPut(keyName,v);
    }

    /// Store a datetime as hash value (HashDatetime)
    /// @return the previous value of the key or NULL if there wasnt one 
    HashValue* hPutDatetime(string keyName,datetime i) {
        HashDatetime *v = new HashDatetime(i);
        return hPut(keyName,v);
    }

    /// Delete an entry from the hash.
    bool hDel(string keyName) {

        bool found = false;
        clearError();

        if (find(keyName)) {
            HashEntry *next = _foundEntry._next;
            if (_foundPrev != NULL) {
                //Remove entry from the middle of the list.
                _foundPrev._next = next;
            } else {
                // remove from head of list
                _buckets[_foundIndex] = next;
            }

            if (_adoptValues && _foundEntry._val != NULL&& CheckPointer(_foundEntry._val) == POINTER_DYNAMIC) {
                delete _foundEntry._val;
            }
            delete _foundEntry;
            _hashEntryCount--;
            found=true;

        }
        return found;
    }
};
uint Hash::_primes[] = {
    17, 53, 97, 193, 389,
    769, 1543, 3079, 6151,
    12289, 24593, 49157, 98317,
    196613, 393241, 786433, 1572869,
    3145739, 6291469, 12582917, 25165843,
    50331653, 100663319, 201326611, 402653189,
    805306457, 1610612741};

/// Class to iterate over a Hash using ...
/// <pre>
///   HashLoop *l
///   for (l = new HashLoop(h) ; l.hasNext() ; l.next()  ) {
///       string key = l.key();
///       MyClass *c = l.val();
///   }
///   delete l;
/// </pre>
class HashLoop {
    private:
        uint _index;
        HashEntry *_currentEntry;
        Hash *_hash;

    public:
        /// Create iterator for a hash - move to first item
        HashLoop(Hash *h) {
            setHash(h);
        }
        ~HashLoop() {};

        /// Clear current state and move to first item (if any).
        void reset() {
            _index=0;
            _currentEntry = _hash.getEntry(_index);

            // Move to first item
            if (_currentEntry == NULL) {
                next();
            }
        }

        /// Change the hash over which to iterate.
        void setHash(Hash *h) {
            _hash = h;
            reset();
        }

        /// Check if more items.
        bool hasNext() {
            bool ret = ( _currentEntry != NULL);
            //config("hasNext=",ret);
            return ret;
        }

        /// Move to next item.
        void next() {

            //config("next : index = ",_index);

            // Advance
            if (_currentEntry != NULL) {
                _currentEntry = _currentEntry._next;
            }

            // Keep advancing if _currentEntry is null
            while (_currentEntry==NULL) {
                _index++;
                if (_index >= _hash.getSlots() ) return ;
                _currentEntry = _hash.getEntry(_index);
            }
        }

        /// Return the key name of the current item.
        string key() {
            if (_currentEntry != NULL) {
                return _currentEntry._key;
            } else {
                return NULL;
            }
        }

        /// Return the value.
        HashValue *val() {
            if (_currentEntry != NULL) {
                return _currentEntry._val;
            } else {
                return NULL;
            }
        }

        /// Convenience functions for retriving int from a current HashInt entry
        int valInt() {
            return ((HashInt *)val()).getVal();
        }

        /// Convenience functions for retriving int from a current HashString entry
        string valString() {
            return ((HashString *)val()).getVal();
        }

        /// Convenience functions for retriving int from a current HashDouble entry
        double valDouble() {
            return ((HashDouble *)val()).getVal();
        }

        /// Convenience functions for retriving int from a current HashLong entry
        long valLong() {
            return ((HashLong *)val()).getVal();
        }
        /// Convenience functions for retriving int from a current HashDatetime entry
        datetime valDatetime() {
            return ((HashDatetime *)val()).getVal();
        }
};


#endif
