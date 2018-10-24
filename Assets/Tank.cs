using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Tank : NetworkBehaviour {

    // This script will run on ALL clients AND on the server
    // Additionally, one of the clients may be the local authority

	// Use this for initialization
	void Start () {
        gameManager = GameObject.FindObjectOfType<GameManager>();
        transform.Translate(GameObject.FindObjectOfType<Player>().transform.position);
        serverRotation = originalRotation = transform.rotation;
        transform.rotation = serverRotation;
        CmdUpdatePosition(transform.position);
    }

    GameManager gameManager;

    [SyncVar]
    float MovementPerTurn = 5;
    [SyncVar]
    float MovementLeft = 5;

    float waitTime = 0;

    float Speed = 5;
    float TurretSpeed = 180; // Degrees per second
    float TurretPowerSpeed = 10;

    public GameObject CurrentBulletPrefab;
    public Transform TurretPivot;

    public Transform BulletSpawnPoint;
    public float BlockWidth = 1.25f;
    public float BlockHeight = 1.28f;
    public float[] worldConstraints = { -27.5f, 27.5f };

    float turretAngle = 90f;

    float turretPower = 10f;

    [SyncVar]
    Vector3 serverPosition;

    [SyncVar]
    Quaternion originalRotation;

    [SyncVar]
    Quaternion serverRotation;

    [SyncVar]
    float serverTurretAngle;

    Vector3 serverPositionSmoothVelocity;
    Vector3 localPositionSmoothVelocity;
    float serverTurretAngleVelocity;

    static public Tank LocalTank { get; protected set; }

    public bool IsLockedIn { get; protected set; }

    
    void NewTurn()
    {
        // Runs on server? 
        MovementLeft = MovementPerTurn;
    }
	
	// Update is called once per frame
	void Update () {
		
        if( isServer )
        {
            // Maybe we need to do some server-specific checking/maintenance?
            // Example: Have the tank take ongoing damage
        }

        if( hasAuthority )
        {
            // This is MY object.  I can do whatever I want with it and the network
            // will listen.

            LocalTank = this;

            if (!this.gameObject.tag.Equals("Player"))
                this.gameObject.tag = "Player";

            AuthorityUpdate();
        }

        // Are we in the correct position?
        if (hasAuthority == false)
        {
            // We don't directly own this object, so we had better move to the server's
            // position.
            transform.rotation = serverRotation;
            transform.position = Vector3.SmoothDamp(
                transform.position,
                serverPosition,
                ref serverPositionSmoothVelocity,
                0.25f);

            turretAngle = Mathf.SmoothDamp(turretAngle, serverTurretAngle, ref serverTurretAngleVelocity, 0.25f);
            
        }

        // Do generic updates for ALL clients/server -- like animating movements and such
        TurretPivot.localRotation = Quaternion.Euler( 0, 0, turretAngle );
        transform.rotation = serverRotation;

    }

    void AuthorityUpdate()
    {
        if( GameManager.Instance().IsProcessingEvent() )
        {
            // Don't accept player input while events are processing
            return;
        }

        AuthorityUpdateMovement();
        AuthorityUpdateAiming();


        // TODO: Make the power display cooler and de-couple from this code
        GameObject pn_go = GameObject.Find("Power Number"); // This is slow!
        pn_go.GetComponent<UnityEngine.UI.Text>().text = turretPower.ToString("#.00");

    }

    void AuthorityUpdateMovement()
    {
        if (IsLockedIn == true || gameManager.TankCanMove(this) == false)
        {
            return;
        }

        waitTime += Time.deltaTime;
        if (waitTime > 2.5f && MovementLeft != 0)
        {
            
            float movement = Input.GetAxis("Horizontal");
            if (movement > 0)
                movement = BlockWidth;
            if (movement < 0)
                movement = BlockWidth * -1;
            if (movement != 0)
            {
                
                //Some reason, game removed us from grid? Fix:
                
                if ((transform.position.x % BlockWidth) != 0)
                {
                    movement -= ((transform.position.x % BlockWidth));
                }
                // Is this in our world?
                if (((transform.position.x + movement) > worldConstraints[1]) || ((transform.position.x + movement) < worldConstraints[0]))
                {
                    return;
                }
                
                transform.rotation = originalRotation;
                waitTime = 0;
                MovementLeft -= 1;
                Debug.Log(MovementLeft + " moves left.");

                serverRotation = transform.localRotation;
                transform.Translate(movement, 0, 0);
                
                CmdUpdatePosition(transform.position);
            }
   
        }
        if (MovementLeft == 0)
        {
            IsLockedIn = true;
            // and let the server know
            CmdLockIn();
        }





        if (Input.GetKeyUp(KeyCode.Space))
        {
            // Lock in our movement
            IsLockedIn = true;
            // and let the server know
            CmdLockIn();
        }
    }


    void AuthorityUpdateAiming()
    {
        if ( IsLockedIn == true || gameManager.TankCanAim(this) == false )
        {
            return;
        }

        // ANGLE
        float turretMovement = Input.GetAxis("Horizontal") * TurretSpeed * Time.deltaTime;
        if(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            turretMovement *= 0.1f;
        }

        turretAngle = Mathf.Clamp( turretAngle + turretMovement, 0, 180 );
        CmdSetTurretAngle(turretAngle); // Sync angle to server

        // POWER
        float powerChange = Input.GetAxis("Vertical") * TurretPowerSpeed * Time.deltaTime;
        if(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            powerChange *= 0.1f;
        }

        turretPower = Mathf.Clamp( turretPower + powerChange, 0, 20 );
        CmdSetTurretPower(turretPower); // Sync power to server


        if (Input.GetKeyUp(KeyCode.Space))
        {
            // Lock in our shot
            IsLockedIn = true;
            // and let the server know
            CmdLockIn();


        }

    }
    [SyncVar]
    bool canShoot = true;

    public void Fire()
    {
        if (!canShoot)
        { NewTurn(); return; }

        this.transform.position = serverPosition;
        turretAngle = serverTurretAngle;

        Vector2 velocity = new Vector2( 
            turretPower * Mathf.Cos( turretAngle * Mathf.Deg2Rad ),
            turretPower * Mathf.Sin( turretAngle * Mathf.Deg2Rad )
        );

        CmdFireBullet( BulletSpawnPoint.position, velocity );
        NewTurn();
    }

    /*
     *  Added Gravity and Hills 
     * 
     */
    private void FixedUpdate()
    {
        
    }
    void OnTriggerEnter2D(Collider2D collider)
    {
        
        if (hasAuthority == false)
        {
            return;
        }
        if (collider.tag.Equals("HILL_L"))
        {
            
            if (transform.rotation.z == 0)
                transform.Rotate(0, 0, 45);
            serverRotation = transform.rotation;
            CmdUpdatePosition(transform.position);
            canShoot = false;
            return;
        }
        if (collider.tag.Equals("GROUND"))
        {
            serverRotation = originalRotation;
            transform.rotation = serverRotation;
            CmdUpdatePosition(transform.position);
            canShoot = true;
            return;
        }
        if (collider.tag.Equals("HILL_R"))
        {
            
            if(transform.rotation.z == 0)
                transform.Rotate(0,0,-45);
            serverRotation = transform.rotation;
            CmdUpdatePosition(transform.position);
            canShoot = false;
            return;
        }

    }

    [Command]
    void CmdLockIn()
    {
        IsLockedIn = true;
    }

    [Command]
    void CmdSetTurretAngle(float angle)
    {
        // TODO: check for legality
        serverTurretAngle = angle;
    }

    [Command]
    void CmdSetTurretPower(float power)
    {
        // TODO: check for legality
        turretPower = power;
    }

    [Command]
    void CmdFireBullet( Vector2 bulletPosition, Vector2 velocity )
    {
        // TODO: Make sure the position and velocity are legal

        float angle = Mathf.Atan2( velocity.y, velocity.x) * Mathf.Rad2Deg;

        // Create the bullet for the clients
        GameObject go = Instantiate(CurrentBulletPrefab, 
            bulletPosition, 
            Quaternion.Euler(0, 0, angle)
        );
        go.GetComponent<Bullet>().SourceTank = this;

        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        rb.velocity = velocity;

        NetworkServer.Spawn( go );
    }

    [Command]
    void CmdUpdatePosition( Vector3 newPosition )
    {
        // TODO: Check to make sure this move is totally legal,
        // both in term of landscape and movement remaining
        // and finally (and most importantly) the TURN PHASE
        // If an illegal move is spotted, do something like:
        //      RpcFixPosition( serverPosition )
        // and return

        if( gameManager.TankCanMove( this ) == false )
        {
            // According to the server, this tank should not be allowed
            // to move right now.  DO SOMETHING
        }

        serverPosition = newPosition;
    }

    [ClientRpc]
    void RpcFixPosition( Vector3 newPosition )
    {
        // We've received a message from the server to immediately
        // correct this tank's position.
        // This is probably only going to happen if the client tried to 
        // move in some kind of illegal manner.

        transform.position = newPosition;

    }

    [ClientRpc]
    void RpcNewTurn()
    {
        // A new turn has just started

    }


    [ClientRpc]
    public void RpcNewPhase()
    {
        // A new phase has just started

        IsLockedIn = false;
    }


}
