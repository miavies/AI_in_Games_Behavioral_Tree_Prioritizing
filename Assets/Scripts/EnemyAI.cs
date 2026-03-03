using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [SerializeField] private Transform target; //player or target
    [SerializeField] private Transform[] patrolPoints;

    [SerializeField] private float detectionRange;
    [SerializeField] private float loseRange;

    [SerializeField] private float waypointTolerance; // how close is "arrived"
    [SerializeField] private float idleAtWaypointSeconds; //break between patrols

    private NavMeshAgent agent;

    //Behavioral tree basic types
    private enum NodeStates 
    { 
        Success, //node completed its job successfully - the condition was true OR action was Finished - I DID IT
        Failure, //the action can't be executed - I CAN'T DO THIS
        Running  //the node is still in progress - moving, waiting, charging(attack)
    }

    private abstract class Node 
    {
        public abstract NodeStates Tick();
    }

    private class Selector : Node
    {
        private readonly List<Node> children;
        public Selector(List<Node> children) => this.children = children;
        public override NodeStates Tick() //Runs the selector logic once
        {
            foreach (var child in children) //loop through each child node in order
            {
                var state = child.Tick();
                if (state == NodeStates.Success) return NodeStates.Success;
                if (state == NodeStates.Running) return NodeStates.Running;
            }
            return NodeStates.Failure;
        }
    }

    private class Sequence : Node
    {
        private readonly List<Node> children;
        public Sequence(List<Node> children) => this.children = children;
        public override NodeStates Tick()
        {
            foreach (var child in children) //loop through each child node in order
            {
                var state = child.Tick();
                if (state == NodeStates.Failure) return NodeStates.Failure;
                if (state == NodeStates.Running) return NodeStates.Running;
            }
            return NodeStates.Success;
        }
    }

    private class ActionNode : Node
    {
        private readonly Func<Node> action; //stores a function that returns NodeStates (Cond or action)
        public ActionNode(Func<Node> action) => this.action = action; //Constructor sets the delegate
        public override NodeStates Tick() => action();
    }

    //AI STATE VARIABLES
    private bool isChasing;
    private int patrolIndex;
    private float idleTimer;

    //Root behavior tree node
    private Node root; //the top of the node of behavior trees

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        //biuld a small behavioral tree:
        //Root selector
        // - Chase Sequence (if detected --> chase)
        // - Patrol Sequence (if has patrol points --> patrol)
        // - Idle

        var chaseSquence = new Sequence(new List<Node>
        {
            new ActionNode(IsTargetDetected),
            new ActionNode(ChaseTarget)
        });

        var patrolSequence = new Sequence(new List<Node>
        {
            new ActionNode(HasPatrolPoints),
            new ActionNode(Patrol)
        });

        var idleAction = new ActionNode(Idle);

        root = new Selector(new List<Node>         //Build root selector with priority ordering
        {
            chaseSquence,                          //Highest priority: chase
            patrolSequence,                        //Next: patrol
            idleAction                             //Last: idle
        });
    }

    private void Update()
    {
        root.Tick(); //run the behavior tree/decision
    }

    private NodeStates IsTargetDetected()
    {
        if (target == null) return NodeStates.Failure;
        float d = Vector3.Distance(transform.position, target.transform.position);

        if (!isChasing)
        {
            if (d <= detectionRange)
            {
                isChasing = true;
                return NodeStates.Success;
            }
            return NodeStates.Failure;
        }
        else
        {
            if (d <= loseRange) return NodeStates.Success;
            isChasing =false;
            return NodeStates.Failure;
        }
    }

    private NodeStates ChaseTarget()
    {
        if (target == null) return NodeStates.Failure;
        agent.isStopped = false;
        agent.SetDestination(target.position);
        return NodeStates.Running;
    }

    private NodeStates HasPatrolPoints()
    {
        if(patrolPoints == null || patrolPoints.Length == 0) return NodeStates.Failure;
        return NodeStates.Success;
    }

    private NodeStates Patrol()
    {
        if(isChasing)return NodeStates.Failure;
        Transform current = patrolPoints[patrolIndex];
        if(current == null) return NodeStates.Failure;
        
        if(idleTimer > 0f)
        {
            agent.isStopped=true;
            idleTimer -= Time.deltaTime;
            return NodeStates.Running;
        }
        return NodeStates.Failure;
    }
}

